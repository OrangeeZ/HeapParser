using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace HeapParser.MonoHeapStateStats
{
    public class LiveAllocationsByBacktrace
    {
        private readonly MonoHeapState _monoHeapState;

        public LiveAllocationsByBacktrace(MonoHeapState monoHeapState)
        {
            _monoHeapState = monoHeapState;
        }
        
        public void Dump_(MonoHeapState monoHeapState, TextWriter writer, string typeName)
        {
            var isEqualityTest = !typeName.StartsWith("*");
            var finalTypeName = isEqualityTest ? typeName : typeName.Replace("*", string.Empty);

            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in monoHeapState.PtrToBackTraceMapping)
            {
                var backtraceString = monoHeapState.BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsBySize = new Dictionary<ulong, uint>();
            foreach (var each in monoHeapState.LiveObjects.Where(_ => isEqualityTest ? _.Value.Class.Name == finalTypeName : _.Value.Class.Name.Contains(finalTypeName)))
            {
                if (!statsBySize.ContainsKey(each.Value.BackTracePtr))
                {
                    statsBySize[each.Value.BackTracePtr] = 0;
                }

                statsBySize[each.Value.BackTracePtr] += each.Value.Size;
            }

            var statsBySizeList = statsBySize.ToList();
            statsBySizeList.Sort((a, b) => b.Value.CompareTo(a.Value));

            var byteToMb = 1f / (1024 * 1024);
            
            var sizeTotalMb = 0f;
            foreach (var each in statsBySizeList)
            {
                var allocationPerBacktrace = each.Value / byteToMb;
                sizeTotalMb += allocationPerBacktrace;
                writer.WriteLine($"{allocationPerBacktrace} MB : {backtraceToString[each.Key]}");
            }

            Console.WriteLine(sizeTotalMb + " MB");
        }
        
        public void Dump(TextWriter writer, string typeName)
        {
            var eventMap = new Dictionary<CustomEvent, List<MonoHeapState.LiveObject>>();
            var monoType = default(MonoType);
            
            foreach (var each in _monoHeapState.LiveObjects)
            {
                if (monoType == null)
                {
                    if (each.Value.Class.Name == (typeName))
                    {
                        monoType = each.Value.Class;
                    }
                }
                
                if (monoType == each.Value.Class)
                {
                    if (!eventMap.ContainsKey(each.Value.LastCustomEvent))
                    {
                        eventMap[each.Value.LastCustomEvent] = new List<MonoHeapState.LiveObject>();
                    }
                    else
                    {
                        eventMap[each.Value.LastCustomEvent].Add(each.Value);
                    }
                }               
            }

            foreach (var each in eventMap)
            {
                DumpEventMap(writer, each.Key, each.Value);
            }
        }

        private void DumpEventMap(TextWriter writer, CustomEvent customEvent, List<MonoHeapState.LiveObject> liveObjects)
        {
            writer.WriteLine(customEvent.EventName + ":");

            var statsByType = new Dictionary<ulong, int>();

            foreach (var each in liveObjects)
            {
                if (!statsByType.ContainsKey(each.BackTracePtr))
                {
                    statsByType[each.BackTracePtr] = 1;
                }
                else
                {
                    statsByType[each.BackTracePtr] += 1;                    
                }
            }
            
            foreach (var each in statsByType)
            {
                var stacktraceString = string.Empty;
                if (_monoHeapState.PtrToBackTraceMapping.ContainsKey(each.Key))
                {
                    stacktraceString = _monoHeapState.BackTraceToString(_monoHeapState.PtrToBackTraceMapping[each.Key]);
                }
                
                writer.Write("\t\t");
                writer.Write(each.Value);
                writer.Write(" : ");
                writer.Write(stacktraceString);                
                writer.Write("\n");
            }
            
            writer.WriteLine("-----------------------------");

            foreach (var each in liveObjects)
            {
                writer.WriteLine(JsonConvert.SerializeObject(each));
            }
        }
    }
}