using System.IO;
using System.Linq;
using HeapParser;

namespace ConsoleApplication1.MonoHeapStateStats
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

            for (var i = section.StartPtr; i <= section.StartPtr + section.Size; i += section.ObjSize)
            {
                if (_monoHeapState.LiveObjects.TryGetValue(i, out var objectInBlock))
                {
                    writer.WriteLine($"{objectInBlock.Class.Name}:{objectInBlock.ObjectPtr}");

                    if (objectInBlock.Class.Name == "TestScript" || objectInBlock.Class.Name == "TestScript2")
                    {
                        var binaryReader = new BinaryReader(new MemoryStream(section.BlockData));
                        var minAlignment = objectInBlock.Class.MinAlignment;
                        while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                        {
                            var potentialPointer =
                                minAlignment == 4 ? binaryReader.ReadUInt32() : binaryReader.ReadUInt64();
                            if (_monoHeapState.LiveObjects.TryGetValue(potentialPointer, out var objectInObject))
                            {
                                writer.Write($"\t\t{objectInObject.Class.Name}:{objectInObject.ObjectPtr}\n");
                            }
                        }
                    }
                }
            }

            writer.WriteLine("--------------------------------------------");
            writer.WriteLine();
        }
    }
}