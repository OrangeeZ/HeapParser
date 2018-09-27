using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HeapParser.MonoHeapStateStats
{
    public class GarbageCollectionByType
    {
        private readonly MonoHeapState _monoHeapState;

        public GarbageCollectionByType(MonoHeapState monoHeapState)
        {
            _monoHeapState = monoHeapState;
        }

        public void DumpGarbageCollectionsStatsByType(TextWriter writer)
        {
            var backtraceToString = new Dictionary<ulong, string>();

            foreach (var each in _monoHeapState.PtrToBackTraceMapping)
            {
                var backtraceString = _monoHeapState.BackTraceToString(each.Value);

                backtraceToString[each.Key] = backtraceString;
            }

            if (!backtraceToString.ContainsKey(0))
            {
                backtraceToString[0] = string.Empty;
            }

            var statsByCount = new Dictionary<ulong, uint>();
            foreach (var each in _monoHeapState.GarbageCollectedObjects)
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
                if (!_monoHeapState.PtrToClassMapping.TryGetValue(each.Key, out monoClass))
                {
                }

                sizeTotalMb += ((float) each.Value / (1024 * 1024));
                writer.WriteLine(monoClass == null ? "Unknown type" : monoClass.Name + " " + each.Value);
            }

            //Console.WriteLine( sizeTotalMb + " MB" );
        }
    }
}