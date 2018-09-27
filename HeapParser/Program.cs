using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ConsoleApplication1.MonoHeapStateStats;
using Newtonsoft.Json;

namespace ConsoleApplication1
{
    public class BinaryReaderDryWrapper
    {
        public readonly BinaryReader Reader;

        public bool IsDryMode;

        private byte[] _byteBuffer = new byte[1024];
        private char[] _charBuffer = new char[1024];

        public BinaryReaderDryWrapper(BinaryReader reader)
        {
            Reader = reader;
        }

        public uint ReadUInt32()
        {
            return IsDryMode ? FastForwardReader<UInt32>(sizeof(UInt32)) : Reader.ReadUInt32();
        }

        public int ReadInt32()
        {
            return IsDryMode ? FastForwardReader<Int32>(sizeof(Int32)) : Reader.ReadInt32();
        }

        public string ReadString()
        {
            var stringLength = Reader.ReadInt32();

            if (IsDryMode)
            {
                FastForwardReader<byte>(stringLength);
                return string.Empty;
            }

            Reader.Read(_byteBuffer, 0, stringLength);
            Encoding.UTF8.GetChars(_byteBuffer, 0, stringLength, _charBuffer, 0);

            //var charsRead = Reader.Read(buffer, 0, stringLength);
            //if (charsRead != stringLength)
            //{
            //    Console.WriteLine($"{charsRead} != {stringLength}");
            //}

            return new string(_charBuffer, 0, stringLength);
        }

        //        public void Read(char[] charBuffer, int i, int stringLength)
        //        {
        //            throw new NotImplementedException();
        //        }

        public ulong ReadUInt64()
        {
            return IsDryMode ? FastForwardReader<UInt64>(sizeof(UInt64)) : Reader.ReadUInt64();
        }

        public byte ReadByte()
        {
            return IsDryMode ? FastForwardReader<byte>(sizeof(byte)) : Reader.ReadByte();
        }

        public bool ReadBoolean()
        {
            return IsDryMode ? FastForwardReader<Boolean>(sizeof(Boolean)) : Reader.ReadBoolean();
        }

        //        public byte[] ReadRawByteArray()
        //        {
        ////            return IsDryMode ? FastForwardReader<byte[]>(sizeof(int)) : BinaryReader.ReadBytes();
        //        }

        public byte[] ReadBytes(int size)
        {
            return IsDryMode ? FastForwardReader<byte[]>(size) : Reader.ReadBytes(size);
        }

        public int ReadInt16()
        {
            return IsDryMode ? FastForwardReader<Int16>(sizeof(Int16)) : Reader.ReadInt16();
        }

        private T FastForwardReader<T>(int amount)
        {
            Reader.BaseStream.Seek(amount, SeekOrigin.Current);

            return default(T);
        }
    }

    public abstract class HeapDescriptor
    {
        public abstract void LoadFrom(BinaryReaderDryWrapper reader);

        public virtual void DryLoadFrom(BinaryReaderDryWrapper reader)
        {
            reader.IsDryMode = true;

            LoadFrom(reader);

            reader.IsDryMode = false;
        }

        public virtual void ApplyTo(MonoHeapState monoHeapState)
        {
        }
    }

    [Serializable]
    public class FileWriterStats : HeapDescriptor
    {
        public uint FileSignature;
        public int FileVersion;
        public string FileLabel;
        public ulong Timestamp;
        public byte PointerSize;
        public byte PlatformId;
        public bool IsLogFullyWritten;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            FileSignature = reader.ReadUInt32();
            FileVersion = reader.ReadInt32();

            FileLabel = reader.ReadString();

            Timestamp = reader.ReadUInt64();
            PointerSize = reader.ReadByte();
            PlatformId = reader.ReadByte();

            IsLogFullyWritten = reader.ReadBoolean();
        }
    }

    public class HeapDumpStats : HeapDescriptor
    {
        public uint TotalGcCount;
        public uint TotalTypeCount;
        public uint TotalMethodCount;
        public uint TotalBacktraceCount;
        public uint TotalResizeCount;

        public ulong TotalFramesCount;
        public ulong TotalObjectNewCount;
        public ulong TotalObjectResizesCount;
        public ulong TotalObjectGcsCount;
        public ulong TotalBoehmNewCount;
        public ulong TotalBoehmFreeCount;

        public ulong TotalAllocatedBytes;
        public uint TotalAllocatedObjects;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            TotalGcCount = reader.ReadUInt32();
            TotalTypeCount = reader.ReadUInt32();
            TotalMethodCount = reader.ReadUInt32();
            TotalBacktraceCount = reader.ReadUInt32();
            TotalResizeCount = reader.ReadUInt32();

            TotalFramesCount = reader.ReadUInt64();
            TotalObjectNewCount = reader.ReadUInt64();
            TotalObjectResizesCount = reader.ReadUInt64();
            TotalObjectGcsCount = reader.ReadUInt64();
            TotalBoehmNewCount = reader.ReadUInt64();
            TotalBoehmFreeCount = reader.ReadUInt64();

            TotalAllocatedBytes = reader.ReadUInt64();
            TotalAllocatedObjects = reader.ReadUInt32();
        }
    }

    [Serializable]
    public class HeapMemory : HeapDescriptor
    {
        public uint MemoryTotalHeapBytes;
        public uint MemoryTotalBytesWritten;

        public HeapMemorySection[] HeapMemorySections;

        public HeapMemoryRootSet[] HeapMemoryStaticRoots;

        public HeapMemoryThread[] HeapMemoryThreads;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            var writtenSectionsCount = reader.ReadUInt32();

            MemoryTotalHeapBytes = reader.ReadUInt32();
            MemoryTotalBytesWritten = reader.ReadUInt32();

            var memoryTotalRoots = reader.ReadUInt32();

            var memoryTotalThreads = reader.ReadUInt32();

            HeapMemorySections = new HeapMemorySection[writtenSectionsCount];

            for (var i = 0; i < writtenSectionsCount; ++i)
            {
                EnsureTag((HeapTag)reader.ReadByte(), HeapTag.cTagHeapMemorySection);

                HeapMemorySections[i] = new HeapMemorySection();
                HeapMemorySections[i].LoadFrom(reader);
            }

            EnsureTag((HeapTag)reader.ReadByte(), HeapTag.cTagHeapMemoryRoots);

            HeapMemoryStaticRoots = new HeapMemoryRootSet[memoryTotalRoots];

            for (var i = 0; i < memoryTotalRoots; ++i)
            {
                HeapMemoryStaticRoots[i] = new HeapMemoryRootSet();
                HeapMemoryStaticRoots[i].LoadFrom(reader);
            }

            EnsureTag((HeapTag)reader.ReadByte(), HeapTag.cTagHeapMemoryThreads);

            HeapMemoryThreads = new HeapMemoryThread[memoryTotalThreads];

            for (var i = 0; i < memoryTotalThreads; ++i)
            {
                HeapMemoryThreads[i] = new HeapMemoryThread();
                HeapMemoryThreads[i].LoadFrom(reader);
            }

            EnsureTag((HeapTag)reader.ReadByte(), HeapTag.cTagHeapMemoryEnd);
        }

        public override void DryLoadFrom(BinaryReaderDryWrapper reader)
        {
            LoadFrom(reader);
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.AddHeapMemorySection(this);
        }

        private static void EnsureTag(HeapTag currentTag, HeapTag expectedTag)
        {
            if (currentTag != expectedTag)
            {
                throw new Exception($"Heap dump read error: got {currentTag} expected {expectedTag}");
            }
        }
    }

    [Serializable]
    public class HeapMemorySection : HeapDescriptor
    {
        public ulong StartPtr;
        public ulong EndPtr;

        public HeapSectionBlock[] HeapSectionBlocks;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            StartPtr = reader.ReadUInt64();
            EndPtr = reader.ReadUInt64();
            var blocksWrittenCount = reader.ReadUInt32();

            HeapSectionBlocks = new HeapSectionBlock[blocksWrittenCount];
            for (var i = 0; i < blocksWrittenCount; ++i)
            {
                EnsureTag((HeapTag)reader.ReadByte(), HeapTag.cTagHeapMemorySectionBlock);

                HeapSectionBlocks[i] = new HeapSectionBlock();
                HeapSectionBlocks[i].LoadFrom(reader);
            }
        }

        private static void EnsureTag(HeapTag currentTag, HeapTag expectedTag)
        {
            if (currentTag != expectedTag)
            {
                throw new Exception(
                    string.Format("Heap dump read error: got {0} expected {1}", currentTag, expectedTag));
            }
        }
    }

    // gc_priv.h:1140
    public enum HeapSectionBlockKind : byte
    {
        PtrFree,
        Normal,
        Uncollectable,
        AtomicUncollectable,
        Stubborn
    }

    [Serializable]
    public class HeapSectionBlock : HeapDescriptor
    {
        public ulong StartPtr;
        public uint Size;
        public uint ObjSize;
        public byte BlockKind;
        public bool IsFree;
        public byte[] BlockData;///ulong[] ObjectPtrs;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            StartPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
            ObjSize = reader.ReadUInt32();
            BlockKind = reader.ReadByte();
            IsFree = reader.ReadBoolean();

            if (!IsFree)
            {
                BlockData = reader.ReadBytes((int)Size);
            }
        }
    }

    public class HeapMemoryRootSet : HeapDescriptor
    {
        public ulong StartPtr;
        public ulong EndPtr;
        public ulong Size;
        public byte[] RootSetRaw; // TODO: check what actually lies in root sets

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            StartPtr = reader.ReadUInt64();
            EndPtr = reader.ReadUInt64();
            Size = reader.ReadUInt64();
            RootSetRaw = reader.ReadBytes((int) Size);

//            var ptrSize = 8u; // Was 4
//            var ptrCount = Size / ptrSize;
//            ObjectPtrs = new ulong[ptrCount];
//
//            for (var i = 0u; i < ptrCount; ++i)
//            {
//                ObjectPtrs[i] = reader.ReadUInt32();
//            }
        }
    }

    public class HeapMemoryThread : HeapDescriptor
    {
        public int ThreadId;
        public ulong StackPtr;
        public uint StackSize;
        public uint RegistersSize;

        public byte[] RawStackMemory;
        public byte[] RawRegistersMemory;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            ThreadId = reader.ReadInt32();
            StackPtr = reader.ReadUInt64();
            StackSize = reader.ReadUInt32();
            RegistersSize = reader.ReadUInt32();

            RawStackMemory = reader.ReadBytes((int)StackSize);
            RawRegistersMemory = reader.ReadBytes((int)RegistersSize);
        }

        //Disable dry run since length reads aren't sequential with byte arrays reads
        public override void DryLoadFrom(BinaryReaderDryWrapper reader)
        {
            LoadFrom(reader);
        }
    }

    public class BoehmAllocation : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong AllocatedObjectPtr;
        public uint Size;
        public uint StacktraceHash;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            AllocatedObjectPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
            StacktraceHash = reader.ReadUInt32();
        }
    }

    public class BoehmAllocationStacktrace : HeapDescriptor
    {
        public uint StacktraceHash;
        public string StacktraceBuffer;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            StacktraceHash = reader.ReadUInt32();
            StacktraceBuffer = reader.ReadString();
        }
    }

    public class GarbageCollectionAccountant : HeapDescriptor
    {
        public ulong BackTracePtr;
        public ulong ClassPtr;
        public uint NumberOfAllocatedObjects;
        public ulong NunberOfAllocatedBytes;
        public uint AllocatedTotalAge;
        public uint AllocatedTotalWeight;
        public uint NumberOfLiveObjects;
        public uint NumberOfLiveBytes;
        public uint LiveTotalAge;
        public uint LiveTotalWeight;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            BackTracePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
            NumberOfAllocatedObjects = reader.ReadUInt32();
            NunberOfAllocatedBytes = reader.ReadUInt64();
            AllocatedTotalAge = reader.ReadUInt32();
            AllocatedTotalWeight = reader.ReadUInt32();
            NumberOfLiveObjects = reader.ReadUInt32();
            NumberOfLiveBytes = reader.ReadUInt32();
            LiveTotalAge = reader.ReadUInt32();
            LiveTotalWeight = reader.ReadUInt32();
        }
    }

    public class MonoGarbageCollect : HeapDescriptor
    {
        public int TotalGcCount;
        public ulong Timestamp;
        public ulong TotalLiveBytesBefore;
        public uint TotalLiveObjectsBefore;
        public ulong TotalLiveBytesAfter;
        public uint TotalLiveObjectsAfter;
        public GarbageCollectionAccountant[] Accountants;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            TotalGcCount = reader.ReadInt32();
            Timestamp = reader.ReadUInt64();
            TotalLiveBytesBefore = reader.ReadUInt64();
            TotalLiveObjectsBefore = reader.ReadUInt32();

            var numberOfAccountants = reader.ReadUInt32();

            Accountants = new GarbageCollectionAccountant[numberOfAccountants];
            for (var i = 0; i < numberOfAccountants; ++i)
            {
                Accountants[i] = new GarbageCollectionAccountant();
                Accountants[i].LoadFrom(reader);
            }

            TotalLiveBytesAfter = reader.ReadUInt64();
            TotalLiveObjectsAfter = reader.ReadUInt32();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.GarbageCollections.AddLast(this);
        }

        public override void DryLoadFrom(BinaryReaderDryWrapper reader)
        {
            LoadFrom(reader);
        }
    }

    public class MonoHeapSize : HeapDescriptor
    {
        public ulong HeapSize;
        public ulong HeapUsedSize;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            HeapSize = reader.ReadUInt64();
            HeapUsedSize = reader.ReadUInt64();
        }
    }

    public class MonoType : HeapDescriptor
    {
        public ulong ClassPtr;
        public string Name;
        public byte Flags;
        public uint Size;
        public uint MinAlignment;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            ClassPtr = reader.ReadUInt64();

            Name = reader.ReadString();

            Flags = reader.ReadByte();
            Size = reader.ReadUInt32();
            MinAlignment = reader.ReadUInt32();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.PtrToClassMapping[ClassPtr] = this;
        }
    }

    public class MonoVTable : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong VTablePtr;
        public ulong ClassPtr;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            VTablePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
        }
    }

    public class BackTraceStackFrame : HeapDescriptor
    {
        public ulong MethodPtr;
        public uint DebugLine;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            MethodPtr = reader.ReadUInt64();
            DebugLine = reader.ReadUInt32();
        }
    }

    public class MonoBackTrace : HeapDescriptor
    {
        public ulong BackTracePtr;
        public ulong Timestamp;

        public BackTraceStackFrame[] StackFrames;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            BackTracePtr = reader.ReadUInt64();
            Timestamp = reader.ReadUInt64();

            var stackFrameCount = reader.ReadInt16();

            StackFrames = new BackTraceStackFrame[stackFrameCount];
            for (var i = 0; i < stackFrameCount; ++i)
            {
                StackFrames[i] = new BackTraceStackFrame();
                StackFrames[i].LoadFrom(reader);
            }
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.PtrToBackTraceMapping[BackTracePtr] = this;
        }
    }

    public class BackTraceTypeLink : HeapDescriptor
    {
        public ulong BackTracePtr;
        public ulong ClassPtr;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            BackTracePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
        }
    }

    public class MonoObjectNew : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong BackTracePtr;
        public ulong ClassPtr;
        public ulong ObjectPtr;
        public uint Size;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            BackTracePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
            ObjectPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.AddLiveObject(this);
        }
    }

    public class MonoMethod : HeapDescriptor
    {
        public ulong MethodPtr;
        public ulong ClassPtr;
        public string Name;
        public string SourceFileName;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            MethodPtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();

            Name = reader.ReadString();
            SourceFileName = reader.ReadString();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.PtrToMethodMapping[MethodPtr] = this;
        }
    }

    public class MonoObjectGarbageCollected : HeapDescriptor
    {
        public ulong BackTracePtr;
        public ulong ClassPtr;
        public ulong ObjectPtr;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            BackTracePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
            ObjectPtr = reader.ReadUInt64();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.RemoveLiveObject(this);
            monoHeapState.GarbageCollectedObjects.AddLast(this);
        }
    }

    class MonoHeapResize : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong NewSize;
        public ulong TotalLiveBytes;
        public uint TotalLiveObjects;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            NewSize = reader.ReadUInt64();
            TotalLiveBytes = reader.ReadUInt64();
            TotalLiveObjects = reader.ReadUInt32();
        }
    }

    public class MonoObjectResize : HeapDescriptor
    {
        public ulong BackTracePtr;
        public ulong ClassPtr;
        public ulong ObjectPtr;
        public uint Size;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            BackTracePtr = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
            ObjectPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.ResizeLiveObject(this);
        }
    }

    public class CustomEvent : HeapDescriptor
    {
        public ulong Timestamp;
        public string EventName;

        public CustomEvent(string eventName)
        {
            Timestamp = 0;
            EventName = eventName;
        }

        public CustomEvent()
        {
        }

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            EventName = reader.ReadString();
        }
    }

    public class MonoStaticClassAllocation : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong ClassPtr;
        public ulong ObjectPtr; //TODO: verify this; from callstack it does look like static object ref, but who knows
        public uint Size;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            ClassPtr = reader.ReadUInt64();
            ObjectPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
        }

        public override void ApplyTo(MonoHeapState monoHeapState)
        {
            monoHeapState.AddLiveObject(this);
        }
    }

    public class MonoThreadTableResize : HeapDescriptor
    {
        public ulong Timestamp;
        public ulong TablePtr;
        public uint TableCount;
        public uint TableSize;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            TablePtr = reader.ReadUInt64();
            TableCount = reader.ReadUInt32();
            TableSize = reader.ReadUInt32();
        }
    }

    public class MonoThreadStaticClassAllocation : HeapDescriptor
    {
        //TODO: verify that this is indeed allocation of statics by threads

        public ulong Timestamp;
        public ulong ObjectPtr; //TODO: verify this; from callstack it does look like static object ref, but who knows
        public uint Size;

        public override void LoadFrom(BinaryReaderDryWrapper reader)
        {
            Timestamp = reader.ReadUInt64();
            ObjectPtr = reader.ReadUInt64();
            Size = reader.ReadUInt32();
        }
    }

    public enum HeapTag
    {
        //        cFileSignature = 0x4EABB055,
        cFileVersion = 3,

        cTagNone = 0,

        MonoType,
        MonoMethod,
        MonoBackTrace,

        MonoGarbageCollect,
        MonoHeapResize,
        MonoObjectNew,
        MonoObjectResize,
        MonoObjectGarbageCollected,
        MonoHeapSize,
        HeapMemory,
        cTagHeapMemoryEnd,
        cTagHeapMemorySection,
        cTagHeapMemorySectionBlock,
        cTagHeapMemoryRoots,
        cTagHeapMemoryThreads,
        BoehmAllocation,
        cTagBoehmFree,
        MonoVTable,
        MonoStaticClassAllocation,
        MonoThreadTableResize,
        MonoThreadStaticClassAllocation,
        BackTraceTypeLink,
        BoehmAllocationStacktrace,

        cTagEos = byte.MaxValue,

        CustomEvent = cTagEos - 1,
        cTagAppResignActive = cTagEos - 2,
        cTagAppBecomeActive = cTagEos - 3,
        cTagNewFrame = cTagEos - 4,
        cTagAppMemoryStats = cTagEos - 5,
    };

    class HeapTagParser
    {
        private Stream _inputStream;

        private List<HeapDescriptor> _heapDescriptors;

        private List<CustomEvent> _heapCustomEvents;
        private List<Tuple<long, CustomEvent>> _heapCustomEventsAndPositions;
        private List<MonoGarbageCollect> _garbageCollectionEvents;

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
            _heapDescriptors = new List<HeapDescriptor>();
            _heapCustomEvents = new List<CustomEvent>();
            _heapCustomEventsAndPositions = new List<Tuple<long, CustomEvent>>();

            var binaryReader = new BinaryReader(_inputStream, Encoding.UTF8);
            var binaryReaderWrapper = new BinaryReaderDryWrapper(binaryReader);

            var writerStats = new FileWriterStats();
            writerStats.LoadFrom(binaryReaderWrapper);

            _heapDescriptors.Add(writerStats);

            Console.WriteLine(JsonConvert.SerializeObject(writerStats));

            var dumpStats = new HeapDumpStats();
            dumpStats.LoadFrom(binaryReaderWrapper);

            _heapDescriptors.Add(dumpStats);

            Console.WriteLine(JsonConvert.SerializeObject(dumpStats));

            HeapState = new MonoHeapState();

            var dryRunExceptions = new bool[_heapDescriptorFactory.MatchingTypes.Length];
            dryRunExceptions[(int)HeapTag.CustomEvent] = true;
            dryRunExceptions[(int)HeapTag.MonoType] = true;
            dryRunExceptions[(int)HeapTag.MonoMethod] = true;
            dryRunExceptions[(int)HeapTag.MonoBackTrace] = true;
            dryRunExceptions[(int)HeapTag.BackTraceTypeLink] = true;

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
                    _heapCustomEvents.Add(each.Descriptor as CustomEvent);
                    _heapCustomEventsAndPositions.Add(new Tuple<long, CustomEvent>(binaryReaderWrapper.Reader.BaseStream.Position, each.Descriptor as CustomEvent));
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

                    each.Descriptor.ApplyTo(HeapState);
                    
                    if (each.Tag == HeapTag.CustomEvent && (each.Descriptor as CustomEvent).Timestamp == toEvent.Timestamp)
                    {
                        var customEvent = (each.Descriptor as CustomEvent);
                        Console.WriteLine($"Hit {customEvent.EventName}:{customEvent.Timestamp}");
                        break;
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

        private IEnumerable<HeapDescriptorData> GetHeapDescriptors(BinaryReaderDryWrapper reader, bool[] dryRunExceptions)
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
                var tag = (HeapTag)reader.ReadByte();
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
                                _heapDescriptorInfo.Sizes[_heapDescriptorFactory.MatchingTypes[(int)tag]];

                            if (dryRunExceptions[(int)tag] || expectedSize == -1)
                            {
                                try
                                {
                                    descriptor = _heapDescriptorFactory.GetInstance(tag);
                                }
                                catch (Exception e)
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
                            catch (Exception e)
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
                new CustomEvent("File end") { Timestamp = (ulong)reader.Reader.BaseStream.Length });
        }

        public List<HeapDescriptor> GetHeapDescriptors()
        {
            return _heapDescriptors;
        }

        public IList<CustomEvent> GetCustomEvents()
        {
            return _heapCustomEvents;
        }
    }

    class Program
    {
        private static CustomEvent SelectTargetCustomEvent(IList<CustomEvent> customEvents)
        {
            if (customEvents.Count == 0)
            {
                return null;
            }

            var resultIndex = -1;

            Console.WriteLine("Select event to replay heap to: ");

            while (resultIndex < 0 || resultIndex >= customEvents.Count + 1)
            {
                for (var i = 0; i < customEvents.Count; ++i)
                {
                    Console.WriteLine(i.ToString() + " " + customEvents[i].EventName);
                }

                int.TryParse(Console.ReadLine(), out resultIndex);
            }

            return customEvents[resultIndex];
        }

        private static void DumpAllocationByType(HeapTagParser heapTagParser, MonoHeapState monoHeapState, string dumpFilePath)
        {
            using (var outStream = new FileStream(dumpFilePath + ".AllocationByBacktrace.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                monoHeapState.DumpMethodAllocationStatsByBacktrace(streamWriter, staticsOnly: false);

                streamWriter.Close();
            }

            using (var outStream = new FileStream(dumpFilePath + ".StaticAllocationByBacktrace.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                monoHeapState.DumpMethodAllocationStatsByBacktrace(streamWriter, staticsOnly: true);

                streamWriter.Close();
            }

            using (var outStream = new FileStream(dumpFilePath + ".TotalAllocationByType.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                monoHeapState.DumpTotalMethodAllocationStatsByType(streamWriter);

                streamWriter.Close();
            }

            using (var outStream = new FileStream(dumpFilePath + ".AllocationByType.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                monoHeapState.DumpMethodAllocationStatsByType(streamWriter);

                streamWriter.Close();
            }
            
            using (var outStream = new FileStream(dumpFilePath + ".MemoryHeapParseResults.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                new MemoryDumpParser(monoHeapState).DumpMemoryHeapParseResults(streamWriter);

                streamWriter.Close();
            }

            while (true)
            {
                Console.WriteLine("Select types to dump live allocation stats for: ");
                var typeNames = Console.ReadLine();

                foreach (var each in typeNames.Split(','))
                {
                    var fileName = each.Replace("*", "_");
                    using (var outStream = new FileStream(dumpFilePath + ".LiveAllocationByType." + fileName + ".txt", FileMode.Create))
                    {
                        var streamWriter = new StreamWriter(outStream);

                        new LiveMethodAllocationsByType().DumpLiveMethodAllocationStatsByType(monoHeapState, streamWriter, each);

                        streamWriter.Close();
                    }
                }
            }
        }

        private static void DumpGarbageCollectionByType(HeapTagParser heapTagParser, MonoHeapState monoHeapState,
            string dumpFilePath)
        {
            Console.WriteLine("Dumping garbage collection stats by type...");

            using (var outStream = new FileStream(dumpFilePath + ".GarbageCollectionsByType.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                monoHeapState.DumpGarbageCollectionsStatsByType(streamWriter);

                streamWriter.Close();
            }
        }

        static void Main(string[] args)
        {
            using (var stream = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.None, int.MaxValue / 2))
            {
                var heapTagParser = new HeapTagParser(stream);

                heapTagParser.PerformHeapFirstPass();

                var fromTag = SelectTargetCustomEvent(heapTagParser.GetCustomEvents());
                var toTag = SelectTargetCustomEvent(heapTagParser.GetCustomEvents());

                heapTagParser.PerformHeapSecondPass(fromTag, toTag);

                DumpGarbageCollectionByType(heapTagParser, heapTagParser.HeapState, args[0]);

                DumpAllocationByType(heapTagParser, heapTagParser.HeapState, args[0]);
            }
        }
    }
}