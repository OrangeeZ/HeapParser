using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HeapParser.MonoHeapStateStats;

namespace HeapParser
{
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
        public byte[] BlockData;

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

    public class MonoHeapResize : HeapDescriptor
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

        private static void DumpAllocationByType(MonoHeapState monoHeapState, string dumpFilePath)
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

        private static void DumpGarbageCollectionByType(MonoHeapState monoHeapState,
            string dumpFilePath)
        {
            Console.WriteLine("Dumping garbage collection stats by type...");

            using (var outStream = new FileStream(dumpFilePath + ".GarbageCollectionsByType.txt", FileMode.Create))
            {
                var streamWriter = new StreamWriter(outStream);

                new GarbageCollectionByType(monoHeapState).DumpGarbageCollectionsStatsByType(streamWriter);

                streamWriter.Close();
            }
        }

        static void Main(string[] args)
        {
            var heapTagParser = default(HeapTagParser);

            using (var stream = new FileStream(args[0], FileMode.Open, FileAccess.Read, FileShare.None, int.MaxValue / 2))
            {
                heapTagParser = new HeapTagParser(stream);

                heapTagParser.PerformHeapFirstPass();

                var fromTag = SelectTargetCustomEvent(heapTagParser.GetCustomEvents());
                var toTag = SelectTargetCustomEvent(heapTagParser.GetCustomEvents());

                heapTagParser.PerformHeapSecondPass(fromTag, toTag);
            }
            
            DumpGarbageCollectionByType(heapTagParser.HeapState, args[0]);

            DumpAllocationByType(heapTagParser.HeapState, args[0]);
        }
    }
}