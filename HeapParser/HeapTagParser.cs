using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace HeapParser
{
    class HeapTagParser
    {
        private Stream _inputStream;

        private List<CustomEvent> _heapCustomEvents;
        private List<Tuple<long, CustomEvent>> _heapCustomEventsAndPositions;

        private HeapDescriptorInfo _heapDescriptorInfo;
        private HeapDescriptorFactory _heapDescriptorFactory;

        public MonoHeapState HeapState { get; private set; }

        public HeapTagParser(Stream inputStream)
        {
            _inputStream = inputStream;

            _heapDescriptorInfo = new HeapDescriptorInfo();
            _heapDescriptorInfo.Initialize();

            _heapDescriptorFactory = new HeapDescriptorFactory(_heapDescriptorInfo);
        }

        //First pass only reads custom events, new type/class/backtrace definitions
        public void PerformHeapFirstPass()
        {
            _heapCustomEvents = new List<CustomEvent>();
            _heapCustomEventsAndPositions = new List<Tuple<long, CustomEvent>>();

            var binaryReader = new BinaryReader(_inputStream, Encoding.UTF8);
            var binaryReaderWrapper = new BinaryReaderDryWrapper(binaryReader);

            var writerStats = new FileWriterStats();
            writerStats.LoadFrom(binaryReaderWrapper);

            Console.WriteLine(JsonConvert.SerializeObject(writerStats));

            var dumpStats = new HeapDumpStats();
            dumpStats.LoadFrom(binaryReaderWrapper);

            Console.WriteLine(JsonConvert.SerializeObject(dumpStats));

            HeapState = new MonoHeapState();
            HeapState.SetWriterStats(writerStats);

            var dryRunExceptions = new bool[_heapDescriptorFactory.MatchingTypes.Length];
            dryRunExceptions[(int) HeapTag.CustomEvent] = true;
            dryRunExceptions[(int) HeapTag.MonoType] = true;
            dryRunExceptions[(int) HeapTag.MonoMethod] = true;
            dryRunExceptions[(int) HeapTag.MonoBackTrace] = true;
            dryRunExceptions[(int) HeapTag.BackTraceTypeLink] = true;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var each in GetHeapDescriptors(binaryReaderWrapper, dryRunExceptions))
            {
                if (each.Tag == HeapTag.HeapMemory)
                {
                    Console.WriteLine("HEAP MEMORY");
                }

                if (each.Tag == HeapTag.CustomEvent)
                {
                    HeapState.SetLastCustomEvent(each.Descriptor as CustomEvent);
                    _heapCustomEvents.Add(each.Descriptor as CustomEvent);
                    _heapCustomEventsAndPositions.Add(new Tuple<long, CustomEvent>(
                        binaryReaderWrapper.Reader.BaseStream.Position, each.Descriptor as CustomEvent));
                }
                else
                {
                    each.Descriptor.ApplyTo(HeapState);
                }
            }

            Console.WriteLine($"First pass took {stopwatch.ElapsedMilliseconds / 1000} seconds");

            HeapState.PostInitialize();
        }

        public void PerformHeapSecondPass(CustomEvent fromEvent, CustomEvent toEvent)
        {
            var position = _heapCustomEventsAndPositions.First(_ => _.Item2.Timestamp == fromEvent.Timestamp).Item1;

            var binaryReader = new BinaryReader(_inputStream, Encoding.UTF8);
            binaryReader.BaseStream.Position = position;

            var binaryReaderWrapper = new BinaryReaderDryWrapper(binaryReader);

            Console.WriteLine($"From {fromEvent.EventName} to {toEvent.EventName}");
            Console.WriteLine($"Starting position: {position}; stream length: {binaryReader.BaseStream.Length}");

            var heapDescriptorData = default(HeapDescriptorData);

            try
            {
                foreach (var each in GetHeapDescriptors(binaryReaderWrapper, null))
                {
                    heapDescriptorData = each;

                    if (each.Tag == HeapTag.CustomEvent)
                    {
                        HeapState.SetLastCustomEvent(each.Descriptor as CustomEvent);
                    }

                    each.Descriptor.ApplyTo(HeapState);

                    if (each.Tag == HeapTag.CustomEvent)
                    {
                        if ((each.Descriptor as CustomEvent).Timestamp == toEvent.Timestamp)
                        {
                            var customEvent = (each.Descriptor as CustomEvent);
                            Console.WriteLine($"Hit {customEvent.EventName}:{customEvent.Timestamp}");
                            break;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(JsonConvert.SerializeObject(heapDescriptorData));
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("Finished second pass");
        }

        private class HeapDescriptorData
        {
            public HeapTag Tag;
            public long Timestamp;
            public HeapDescriptor Descriptor;

            public HeapDescriptorData SetData(HeapTag tag, long timestamp, HeapDescriptor descriptor)
            {
                Tag = tag;
                Timestamp = timestamp;
                Descriptor = descriptor;

                return this;
            }
        }

        private IEnumerable<HeapDescriptorData> GetHeapDescriptors(BinaryReaderDryWrapper reader,
            bool[] dryRunExceptions)
        {
            var lastLastTag = HeapTag.cTagNone;
            var lastTag = HeapTag.cTagNone;

            var isEos = false;
            var emptyReadBuffer = new byte[1024];

            var result = new HeapDescriptorData();

            yield return result.SetData(HeapTag.CustomEvent, 0, new CustomEvent("File start"));

            var streamLength = reader.Reader.BaseStream.Length;
            while (reader.Reader.BaseStream.Position != streamLength && !isEos)
            {
                var tag = (HeapTag) reader.ReadByte();
                var descriptor = default(HeapDescriptor);

                if (tag == HeapTag.cTagEos)
                {
                    Console.WriteLine("End of stream");
                    isEos = true;
                }

                try
                {
                    if (!isEos)
                    {
                        if (dryRunExceptions != null)
                        {
                            var expectedSize =
                                _heapDescriptorInfo.Sizes[_heapDescriptorFactory.MatchingTypes[(int) tag]];

                            if (dryRunExceptions[(int) tag] || expectedSize == -1)
                            {
                                try
                                {
                                    descriptor = _heapDescriptorFactory.GetInstance(tag);
                                }
                                catch (Exception)
                                {
                                    Console.WriteLine("Previous tag: " + lastLastTag);
                                    Console.WriteLine($"Previous tag: {lastTag}");
                                    Console.WriteLine($"Tag {tag} unsupported; halting");

                                    yield break;
                                }

                                descriptor?.LoadFrom(reader);
                            }
                            else
                            {
                                reader.Reader.Read(emptyReadBuffer, 0, expectedSize);
                            }
                        }
                        else
                        {
                            try
                            {
                                descriptor = _heapDescriptorFactory.GetInstance(tag);
                            }
                            catch (Exception)
                            {
                                Console.WriteLine("Previous tag: " + lastLastTag);
                                Console.WriteLine("Previous tag: " + lastTag);
                                Console.WriteLine("Tag " + tag + " unsupported; halting");

                                yield break;
                            }

                            descriptor?.LoadFrom(reader);
                        }
                    }

                    lastLastTag = lastTag;
                    lastTag = tag;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Previous tag: " + lastLastTag);
                    Console.WriteLine("Previous tag: " + lastTag);
                    Console.WriteLine("Current tag: " + tag);
                    Console.WriteLine(e);

                    descriptor = null;

                    isEos = true;
                }

                if (descriptor != null)
                {
                    yield return result.SetData(tag, reader.Reader.BaseStream.Position, descriptor);
                }
            }

            yield return result.SetData(HeapTag.CustomEvent, reader.Reader.BaseStream.Length,
                new CustomEvent("File end") {Timestamp = (ulong) reader.Reader.BaseStream.Length});
        }

        public IList<CustomEvent> GetCustomEvents()
        {
            return _heapCustomEvents;
        }
    }
}