using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ConsoleApplication1
{
    public class MonoHeapState
    {
        public Dictionary<ulong, MonoType> PtrToClassMapping = new Dictionary<ulong, MonoType>();
        public Dictionary<ulong, MonoMethod> PtrMethodMapping = new Dictionary<ulong, MonoMethod>();
        public Dictionary<ulong, MonoBackTrace> PtrBackTraceMapping = new Dictionary<ulong, MonoBackTrace>();
        public Dictionary<ulong, ulong> PtrBacktraceToPtrClass = new Dictionary<ulong, ulong>();

        public Dictionary<ulong, MonoObjectNew> ObjectPtrToMonoObjectNew = new Dictionary<ulong, MonoObjectNew>();

        public LinkedList<MonoGarbageCollect> GarbageCollections = new LinkedList<MonoGarbageCollect>();
        public LinkedList<MonoObjectGarbageCollected> GarbageCollectedObjects = new LinkedList<MonoObjectGarbageCollected>();

        private Dictionary<ulong, LiveObject> _liveObjects = new Dictionary<ulong, LiveObject>();
        private Dictionary<ulong, ulong> _totalAllocationsPerType = new Dictionary<ulong, ulong>();

        private struct LiveObject
        {
            public MonoType Class;
            public MonoMethod Method;
            public ulong BackTracePtr;

            public ulong ObjectPtr;
            public uint Size;
            public bool IsStatic;
        }

        private ulong? ObjectClassPtr = null;
        private List<HeapMemory> _heapMemorySections = new List<HeapMemory>();

        //public IEnumerable<string> Get

        public void PostInitialize()
        {
            ObjectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
        }

        public void ResizeLiveObject(MonoObjectResize objectResize)
        {
            var liveObject = default(LiveObject);
            if (_liveObjects.TryGetValue(objectResize.ObjectPtr, out liveObject))
            {
                liveObject.Size = objectResize.Size;
                _liveObjects[objectResize.ObjectPtr] = liveObject;
            }
        }

        public void AddLiveObject(MonoObjectNew newObject)
        {
            if (ObjectClassPtr == null)
            {
                ObjectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
            }

            var liveObject = new LiveObject();

            var backtrace = PtrBackTraceMapping[newObject.BackTracePtr];
            var index = Math.Max(backtrace.StackFrames.Length - 2, 0);
            var stackFrame = backtrace.StackFrames.Length > 0 ? backtrace.StackFrames[index] : null;

            if (stackFrame != null)
            {
                var referencingMethodPtr = stackFrame.MethodPtr;
                var referencingMethod = PtrMethodMapping.ContainsKey(referencingMethodPtr)
                    ? PtrMethodMapping[referencingMethodPtr]
                    : default(MonoMethod);
                liveObject.Method = referencingMethod;
            }

            liveObject.Class = PtrToClassMapping[newObject.ClassPtr];
            liveObject.ObjectPtr = newObject.ObjectPtr;
            liveObject.BackTracePtr = newObject.BackTracePtr;

            liveObject.Size = newObject.Size;
            liveObject.IsStatic = false;

            _liveObjects.Add(liveObject.ObjectPtr, liveObject);

            if (!_totalAllocationsPerType.ContainsKey(newObject.ClassPtr))
            {
                _totalAllocationsPerType.Add(newObject.ClassPtr, 0);
            }

            _totalAllocationsPerType[newObject.ClassPtr] += liveObject.Size;
        }

        public void AddLiveObject(MonoStaticClassAllocation newObject)
        {
            if (ObjectClassPtr == null)
            {
                ObjectClassPtr = PtrToClassMapping.FirstOrDefault(_ => _.Value.Name == "object").Key;
            }

            var liveObject = new LiveObject();

            liveObject.Class = PtrToClassMapping[newObject.ClassPtr];
            liveObject.Method = null;
            liveObject.ObjectPtr = newObject.ObjectPtr;

            liveObject.Size = newObject.Size;
            liveObject.IsStatic = true;

            _liveObjects.Add(liveObject.ObjectPtr, liveObject);
        }

        public void RemoveLiveObject(MonoObjectGarbageCollected collectedObject)
        {
            _liveObjects.Remove(collectedObject.ObjectPtr);
        }

        public void DumpMethodAllocationStats(TextWriter writer)
        {
            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            //foreach ( var each in _liveObjects ) {

            //	writer.WriteLine( each.Value.Class.Name + " " + each.Value.Size + " Static: " + each.Value.IsStatic );
            //}

            var statsBySize = new Dictionary<MonoType, uint>();
            foreach (var each in _liveObjects)
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
                var currentAllocationSize = ((float)each.Value / (1024 * 1024));
                sizeTotalMb += currentAllocationSize;
                if (currentAllocationSize > 0.1f)
                {
                    writer.WriteLine(each.Key.Name + " " + currentAllocationSize + " MB");
                }
            }

            Console.WriteLine(sizeTotalMb + " MB");
        }

        public void DumpLiveMethodAllocationStatsByType(TextWriter writer, string typeName)
        {
            var isEqualityTest = !typeName.StartsWith("*");
            var finalTypeName = isEqualityTest ? typeName : typeName.Replace("*", string.Empty);

            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsBySize = new Dictionary<ulong, uint>();
            foreach (var each in _liveObjects.Where(_ =>
                isEqualityTest ? _.Value.Class.Name == finalTypeName : _.Value.Class.Name.Contains(finalTypeName)))
            {
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
            //var monoClass = PtrClassMapping.Values.FirstOrDefault( _ => _.Name == typeName );

            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsBySize = new Dictionary<ulong, uint>();
            foreach (var each in _liveObjects)
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
            //var monoClass = PtrClassMapping.Values.FirstOrDefault( _ => _.Name == typeName );

            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            //foreach ( var each in _liveObjects ) {

            //	writer.WriteLine( each.Value.Class.Name + " " + each.Value.Size + " Static: " + each.Value.IsStatic );
            //}

            var statsBySize = new Dictionary<MonoType, uint>();
            foreach (var each in _liveObjects)
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

        public void DumpMemoryHeapParseResults(TextWriter writer)
        {
            if (_heapMemorySections.Count == 0)
            {
                return;
            }
            
//            foreach (var each in _heapMemorySections)
            {
                ParseHeapMemory(_heapMemorySections[_heapMemorySections.Count - 1], writer);
            }
        }

        private void ParseHeapMemory(HeapMemory heapMemory, TextWriter writer)
        {
            var memorySections = heapMemory.HeapMemorySections;

            foreach (var each in memorySections.SelectMany(_ => _.HeapSectionBlocks))
            {
                ParseMemorySection(each, writer);
            }
        }

        private void ParseMemorySection(HeapSectionBlock section, TextWriter writer)
        {
//            if ((HeapSectionBlockKind)section.BlockKind == HeapSectionBlockKind.PtrFree)
//            {
//                return;
//            }
            
            if (section.IsFree)
            {
                writer.WriteLine("This section is not PtrFree, but IsFree. Skipping.");
                
                return;
            }
            
            writer.WriteLine();
            writer.WriteLine("--------------------------------------------");

//            var binaryReader = new BinaryReader(new MemoryStream(section.BlockData));
            for (var i = section.StartPtr; i <= section.StartPtr + section.Size; i += section.ObjSize)
            {
                if (_liveObjects.TryGetValue(i, out var objectInBlock))
                {
                    writer.WriteLine($"{objectInBlock.Class.Name}:{objectInBlock.ObjectPtr}");//JsonConvert.SerializeObject(objectInBlock));

                    if (objectInBlock.Class.Name == "TestScript" || objectInBlock.Class.Name == "TestScript2")
                    {
                        var binaryReader = new BinaryReader(new MemoryStream(section.BlockData));
                        var minAlignment = objectInBlock.Class.MinAlignment;
                        while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                        {
                            var potentialPointer = binaryReader.ReadUInt64();
                            if (_liveObjects.TryGetValue(potentialPointer, out var objectInObject))
                            {
                                writer.Write($"\t\t{objectInObject.Class.Name}:{objectInObject.ObjectPtr}\n");
                            }

                        }
//                        for (var j = i; j < i + section.ObjSize; j += minAlignment)
//                        {
//                            if (_liveObjects.TryGetValue(i, out var objectInObject))
//                            {
//                                writer.Write("\t\t" + JsonConvert.SerializeObject(objectInObject));
//                            }
//                        }
                    }
                    
                }
            }
            
            writer.WriteLine("--------------------------------------------");
            writer.WriteLine();
        }

        public void DumpGarbageCollectionsStatsByType(TextWriter writer)
        {
            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in PtrBackTraceMapping)
            {
                var backtraceString = BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsByCount = new Dictionary<ulong, uint>();
            foreach (var each in GarbageCollectedObjects)
            {
                if (!statsByCount.ContainsKey(each.ClassPtr))
                {
                    statsByCount[each.ClassPtr] = 0;
                }

                statsByCount[each.ClassPtr] += 1;
            }

            var statsBySizeList = statsByCount.ToList();
            statsBySizeList.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sizeTotalMb = 0f;
            foreach (var each in statsBySizeList)
            {
                var monoClass = default(MonoType);
                if (!PtrToClassMapping.TryGetValue(each.Key, out monoClass))
                {
                }

                sizeTotalMb += ((float)each.Value / (1024 * 1024));
                writer.WriteLine(monoClass == null ? "Unknown type" : monoClass.Name + " " + each.Value);
            }

            //Console.WriteLine( sizeTotalMb + " MB" );
        }

        public void ResetLiveObjects()
        {
            _liveObjects.Clear();
            GarbageCollectedObjects.Clear();
        }

        private string BackTraceToString(MonoBackTrace backtrace)
        {
            var result = new StringBuilder();
            foreach (var each in backtrace.StackFrames)
            {
                var monoMethod = PtrMethodMapping.ContainsKey(each.MethodPtr) ? PtrMethodMapping[each.MethodPtr] : null;

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
            _heapMemorySections.Add(heapMemory);
        }
    }
}