using System.IO;
using System.Linq;
using System.Text;

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

            var stringBuilder = new StringBuilder();
            var charBuffer = new char[1024];
            
            for (var i = section.StartPtr; i <= section.StartPtr + section.Size; i += section.ObjSize)
            {
                if (!_monoHeapState.LiveObjects.TryGetValue(i, out var objectInBlock))
                {
                    continue;
                }

                writer.WriteLine($"{objectInBlock.Class.Name}:{objectInBlock.ObjectPtr}");

                var binaryReader = new BinaryReader(new MemoryStream(section.BlockData));
                var minAlignment = objectInBlock.Class.MinAlignment;
                while (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
                {
                    var potentialPointer = minAlignment == 4 ? binaryReader.ReadUInt32() : binaryReader.ReadUInt64();

                    if (_monoHeapState.LiveObjects.TryGetValue(potentialPointer, out var objectInObject))
                    {
                        stringBuilder.Clear();
                        stringBuilder.Append("\t\t").Append(objectInObject.Class.Name).Append(objectInObject.ObjectPtr);
                        stringBuilder.CopyTo(0, charBuffer, 0, stringBuilder.Length);
                        
                        writer.WriteLine(charBuffer, 0, stringBuilder.Length);
                    }
                }
            }

            writer.WriteLine("--------------------------------------------");
            writer.WriteLine();
        }
    }
}