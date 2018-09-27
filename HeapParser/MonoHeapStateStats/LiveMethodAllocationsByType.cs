using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConsoleApplication1.MonoHeapStateStats
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

            var sizeTotalMb = 0f;
            foreach (var each in statsBySizeList)
            {
                sizeTotalMb += ((float)each.Value / (1024 * 1024));
                writer.WriteLine(backtraceToString[each.Key] + " " + ((float)each.Value / (1024 * 1024)) + " MB");
            }

            Console.WriteLine(sizeTotalMb + " MB");
        }
    }
}