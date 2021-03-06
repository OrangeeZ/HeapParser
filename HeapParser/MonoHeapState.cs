﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace HeapParser
{
    public class MonoHeapState
    {
        public FileWriterStats WriterStats { get; set; }

        public Dictionary<ulong, MonoType> PtrToClassMapping = new Dictionary<ulong, MonoType>();
        public Dictionary<ulong, MonoMethod> PtrToMethodMapping = new Dictionary<ulong, MonoMethod>();
        public Dictionary<ulong, MonoBackTrace> PtrToBackTraceMapping = new Dictionary<ulong, MonoBackTrace>();

        public LinkedList<MonoGarbageCollect> GarbageCollections = new LinkedList<MonoGarbageCollect>();
        public LinkedList<MonoObjectGarbageCollected> GarbageCollectedObjects = new LinkedList<MonoObjectGarbageCollected>();

        public Dictionary<ulong, LiveObject> LiveObjects = new Dictionary<ulong, LiveObject>();
        
        private Dictionary<ulong, ulong> _totalAllocationsPerType = new Dictionary<ulong, ulong>();

        private CustomEvent _lastCustomEvent;

        public struct LiveObject
        {
            public MonoType Class;
            public ulong BackTracePtr;

            public ulong ObjectPtr;
            public uint Size;
            public bool IsStatic;

            public CustomEvent LastCustomEvent;
        }

        public List<HeapMemory> HeapMemorySections = new List<HeapMemory>();

        private ulong? _objectClassPtr = null;

        public void PostInitialize()
        {
            _objectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
        }

        public void SetLastCustomEvent(CustomEvent lastCustomEvent)
        {
            _lastCustomEvent = lastCustomEvent;
        }

        public void ResizeLiveObject(MonoObjectResize objectResize)
        {
            var liveObject = default(LiveObject);
            if (LiveObjects.TryGetValue(objectResize.ObjectPtr, out liveObject))
            {
                liveObject.Size = objectResize.Size;
                LiveObjects[objectResize.ObjectPtr] = liveObject;
            }
        }

        public void AddLiveObject(MonoObjectNew newObject)
        {
            if (_objectClassPtr == null)
            {
                _objectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
            }

            var liveObject = new LiveObject
            {
                Class = PtrToClassMapping[newObject.ClassPtr],
                ObjectPtr = newObject.ObjectPtr,
                BackTracePtr = newObject.BackTracePtr,
                Size = newObject.Size,
                IsStatic = false,
                LastCustomEvent = _lastCustomEvent
            };
            
            LiveObjects.Add(liveObject.ObjectPtr, liveObject);

            if (!_totalAllocationsPerType.ContainsKey(newObject.ClassPtr))
            {
                _totalAllocationsPerType.Add(newObject.ClassPtr, 0);
            }

            _totalAllocationsPerType[newObject.ClassPtr] += liveObject.Size;
        }

        public void AddStaticLiveObject(MonoStaticClassAllocation newObject)
        {
            if (_objectClassPtr == null)
            {
                _objectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
            }

            var liveObject = new LiveObject
            {
                Class = PtrToClassMapping[newObject.ClassPtr],
                ObjectPtr = newObject.ObjectPtr,
                Size = newObject.Size,
                IsStatic = true,
                LastCustomEvent = _lastCustomEvent
            };

            LiveObjects.Add(liveObject.ObjectPtr, liveObject);
        }

        public void RemoveLiveObject(MonoObjectGarbageCollected collectedObject)
        {
            LiveObjects.Remove(collectedObject.ObjectPtr);
        }

        public void DumpTotalMethodAllocationStatsByType(TextWriter writer)
        {
            var byteToMb = 1f / (1024 * 1024);

            var pairs = _totalAllocationsPerType.ToList();
            pairs.Sort((a, b) => b.Value.CompareTo(a.Value));

            foreach (var each in pairs)
            {
                if (!PtrToClassMapping.ContainsKey(each.Key))
                {
                    continue;
                }

                var type = PtrToClassMapping[each.Key];

                writer.WriteLine($"{type.Name}: {each.Value * byteToMb} MB");
            }
        }

        public void DumpMethodAllocationStatsByBacktrace(TextWriter writer, bool staticsOnly)
        {
            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrToBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsBySize = new Dictionary<ulong, uint>();
            foreach (var each in LiveObjects)
            {
                if (staticsOnly && !each.Value.IsStatic)
                {
                    continue;
                }

                if (!statsBySize.ContainsKey(each.Value.BackTracePtr))
                {
                    statsBySize[each.Value.BackTracePtr] = 0;
                }

                statsBySize[each.Value.BackTracePtr] += each.Value.Size;
            }

            var statsBySizeList = statsBySize.ToList();
            statsBySizeList.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sizeTotalMb = 0f;
            foreach (var each in statsBySizeList)
            {
                sizeTotalMb += ((float)each.Value / (1024 * 1024));
                writer.WriteLine(backtraceToString[each.Key] + " " + ((float)each.Value / (1024 * 1024)) + " MB");
            }

            Console.WriteLine(sizeTotalMb + " MB");
        }

        public void DumpMethodAllocationStatsByType(TextWriter writer)
        {
            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrToBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsBySize = new Dictionary<MonoType, uint>();
            foreach (var each in LiveObjects)
            {
                if (!statsBySize.ContainsKey(each.Value.Class))
                {
                    statsBySize[each.Value.Class] = 0;
                }

                statsBySize[each.Value.Class] += each.Value.Size;
            }

            var statsBySizeList = statsBySize.ToList();
            statsBySizeList.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sizeTotalMb = 0f;
            foreach (var each in statsBySizeList)
            {
                sizeTotalMb += ((float)each.Value / (1024 * 1024));
                writer.WriteLine(each.Key.Name + " " + ((float)each.Value / (1024 * 1024)) + " MB");
            }

            Console.WriteLine(sizeTotalMb + " MB");
        }

        public void ResetLiveObjects()
        {
            LiveObjects.Clear();
            GarbageCollectedObjects.Clear();
        }

        public string BackTraceToString(MonoBackTrace backtrace)
        {
            var result = new StringBuilder();
            foreach (var each in backtrace.StackFrames)
            {
                var monoMethod = PtrToMethodMapping.ContainsKey(each.MethodPtr) ? PtrToMethodMapping[each.MethodPtr] : null;

                if (monoMethod == null || monoMethod.Name.Contains("(wrapper"))
                {
                    continue;
                }

                result.Append(monoMethod.Name);
                result.Append("->");
            }

            return result.ToString();
        }

        public void AddHeapMemorySection(HeapMemory heapMemory)
        {
            HeapMemorySections.Add(heapMemory);
        }

        public void SetWriterStats(FileWriterStats writerStats)
        {
            WriterStats = writerStats;
        }
    }
}