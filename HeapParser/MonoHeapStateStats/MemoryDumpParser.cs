using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace HeapParser.MonoHeapStateStats
{
    public class MemoryDumpParser
    {
        private readonly MonoHeapState _monoHeapState;

        public MemoryDumpParser(MonoHeapState monoHeapState)
        {
            _monoHeapState = monoHeapState;
        }

        public void DumpMemoryHeapParseResults(TextWriter writer)
        {
            if (_monoHeapState.HeapMemorySections.Count == 0)
            {
                return;
            }

            ParseHeapMemory(_monoHeapState.HeapMemorySections[_monoHeapState.HeapMemorySections.Count - 1], writer);
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
            if (section.IsFree)
            {
                writer.WriteLine("This section is not PtrFree, but IsFree. Skipping.");

                return;
            }

            writer.WriteLine();
            writer.WriteLine("--------------------------------------------");

            var pointerSize = _monoHeapState.WriterStats.PointerSize;
            var currentPtr = section.StartPtr;
            for (var i = 0; i < section.Size; i += (int) section.ObjSize, currentPtr += section.ObjSize)
            {
                if (!_monoHeapState.LiveObjects.TryGetValue(currentPtr, out var objectInBlock))
                {
                    continue;
                }

                writer.WriteLine($"{objectInBlock.Class.Name}, IsStatic = {objectInBlock.IsStatic} : {objectInBlock.ObjectPtr}");

                if (objectInBlock.Class.Name == "MW2.UI.Menu.PopupMenu.PopupMenuScreen")
                {
                    Console.WriteLine(JsonConvert.SerializeObject(objectInBlock));
                    Console.WriteLine(section.ObjSize);
                }

                var minAlignment = objectInBlock.Class.MinAlignment;
                var startPosition = i;
                var endPosition = startPosition + section.ObjSize;

                if (endPosition > section.Size)
                {
                    break;
                }

                while (startPosition + pointerSize <= endPosition)
                {
                    var potentialPointer = pointerSize == 4 ? BitConverter.ToUInt32(section.BlockData, startPosition) : BitConverter.ToUInt64(section.BlockData, startPosition);

                    if (_monoHeapState.LiveObjects.TryGetValue(potentialPointer, out var objectInObject))
                    {
                        writer.Write("\t\t");
                        writer.Write(objectInObject.Class.Name);
                        writer.Write(" : ");
                        writer.Write(objectInObject.ObjectPtr);
                        writer.Write("\n");

                        startPosition += pointerSize;
                    }
                    else
                    {
                        startPosition += (int) minAlignment;
                    }
                }
            }

            writer.WriteLine("--------------------------------------------");
            writer.WriteLine();
        }
    }
}