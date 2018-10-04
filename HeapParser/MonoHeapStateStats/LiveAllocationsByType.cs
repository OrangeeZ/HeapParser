using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HeapParser.MonoHeapStateStats
{
    public class LiveAllocationsByType
    {
        private readonly MonoHeapState _monoHeapState;

        public LiveAllocationsByType(MonoHeapState monoHeapState)
        {
            _monoHeapState = monoHeapState;
        }

        public void Dump(TextWriter writer)
        {
            var eventMap = new Dictionary<CustomEvent, List<MonoHeapState.LiveObject>>();
            foreach (var each in _monoHeapState.LiveObjects)
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

            foreach (var each in eventMap)
            {
                DumpEventMap(writer, each.Key, each.Value);
            }
        }

        private void DumpEventMap(TextWriter writer, CustomEvent customEvent, List<MonoHeapState.LiveObject> liveObjects)
        {
            writer.WriteLine(customEvent.EventName + ":");

            var statsByType = new Dictionary<MonoType, int>();

            foreach (var each in liveObjects)
            {
                if (!statsByType.ContainsKey(each.Class))
                {
                    statsByType[each.Class] = 1;
                }
                else
                {
                    statsByType[each.Class] += 1;                    
                }
            }
            
            foreach (var each in statsByType)
            {
                writer.Write("\t\t");
                writer.Write(each.Key.Name);
                writer.Write(" : ");
                writer.Write(each.Value);
                writer.Write("\n");
            }
        }
    }
}