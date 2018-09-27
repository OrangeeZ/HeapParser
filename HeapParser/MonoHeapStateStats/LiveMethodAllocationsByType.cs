using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HeapParser.MonoHeapStateStats
{
    public class LiveMethodAllocationsByType
    {
        public void DumpLiveMethodAllocationStatsByType(MonoHeapState monoHeapState, TextWriter writer, string typeName)
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
            foreach (var each in monoHeapState.LiveObjects.Where(_ =>
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
    }
}