using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

using Esprima.Ast;

using Syroot.BinaryData.Memory;

namespace XenoTools.Script.Entities
{
    public class DebugInfo
    {
        public List<string> FileNames { get; set; } = [];
        public Dictionary<int, LineInfo> Lines { get; set; } = [];

        public record LineInfo(int SourceNameID, short LineNumber, int InstructionAddr);

        public void Read(ref SpanReader sr)
        {
            int baseOffset = sr.Position;

            int StaticSymbolsOfs = sr.ReadInt32();
            int LocalSymsOfs = sr.ReadInt32();
            int ArgSymsOfs = sr.ReadInt32();
            int FileNamesOfs = sr.ReadInt32();
            int LineInfoOfs = sr.ReadInt32();

            ReadFileNames(ref sr, (int)baseOffset + FileNamesOfs);
            ReadLineNumberInfo(ref sr, (int)baseOffset + LineInfoOfs);
        }

        private void ReadFileNames(ref SpanReader sr, int offset)
        {
            sr.Position = offset;

            int strRelOffset = sr.ReadInt32();
            int numStrs = sr.ReadInt32();

            int[] offsets = new int[numStrs];
            for (int i = 0; i < numStrs; i++)
                offsets[i] = sr.ReadInt16();

            for (int i = 0; i < numStrs; i++)
            {
                sr.Position = (int)(offset + strRelOffset + offsets[i]);
                string id = sr.ReadString0();
                FileNames.Add(id);
            }
        }

        private void ReadLineNumberInfo(ref SpanReader sr, int offset)
        {
            sr.Position = offset;

            int strRelOffset = sr.ReadInt32();
            int numEntries = sr.ReadInt32();

            for (int i = 0; i < numEntries; i++)
            {
                short sourceNameID = sr.ReadInt16();
                short lineNumber = sr.ReadInt16();
                int instructionOfs = sr.ReadInt32();

                Lines.Add(instructionOfs, new LineInfo(sourceNameID, lineNumber, instructionOfs));
            }
        }
    }
}
