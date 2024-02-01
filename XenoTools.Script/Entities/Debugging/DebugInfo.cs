using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Threading.Tasks;

using Esprima.Ast;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

namespace XenoTools.Script.Entities.Debugging
{
    public class DebugInfo
    {
        public Dictionary<int, DebugInfoVariable> StaticSyms { get; set; } = [];
        public List<DebugInfoFunctionLocals> FunctionLocalSymbols { get; set; } = [];

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

            if (StaticSymbolsOfs != 0)
                ReadStaticSymbols(ref sr, baseOffset + StaticSymbolsOfs);

            if (LocalSymsOfs != 0)
                ReadFunctionLocalsSymbols(ref sr, baseOffset + LocalSymsOfs);

            if (LocalSymsOfs != 0)
                ReadFileNames(ref sr, baseOffset + FileNamesOfs);

            if (LineInfoOfs != 0)
                ReadLineNumberInfo(ref sr, baseOffset + LineInfoOfs);
        }

        private void ReadStaticSymbols(ref SpanReader sr, int offset)
        {
            sr.Position = offset;

            int strRelOffset = sr.ReadInt32();
            int numEntries = sr.ReadInt32();

            for (int i = 0; i < numEntries; i++)
            {
                var staticSym = new DebugInfoVariable();
                staticSym.Read(ref sr);
                StaticSyms.Add(staticSym.ID, staticSym);
            }
        }

        private void ReadFunctionLocalsSymbols(ref SpanReader sr, int offset)
        {
            sr.Position = offset;

            int strRelOffset = sr.ReadInt32();
            int numEntries = sr.ReadInt32();

            int baseOffset = sr.Position;
            (short offset, short count)[] funcs = new (short, short)[numEntries];
            for (int i = 0; i < numEntries; i++)
                funcs[i] = (sr.ReadInt16(), sr.ReadInt16());

            for (int i = 0; i < numEntries; i++)
            {
                var funcLocalSyms = new DebugInfoFunctionLocals();
                FunctionLocalSymbols.Add(funcLocalSyms);

                if (funcs[i].count == 0)
                    continue;

                sr.Position = offset + strRelOffset + funcs[i].offset;
                for (int j = 0; j < funcs[i].count; j++)
                {
                    var var = new DebugInfoVariable();
                    var.Read(ref sr);
                    funcLocalSyms.Locals.Add(var.ID, var);
                }
                
            }
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
                sr.Position = offset + strRelOffset + offsets[i];
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

        public void Write(BinaryStream bs)
        {
            int baseOffset = (int)bs.Position;
            bs.Position += 0x14;

            WriteStaticSymbols(bs, baseOffset);
            WriteFunctionLocalsSymbols(bs, baseOffset);
        }

        private void WriteStaticSymbols(BinaryStream bs, int baseOffset)
        {
            int staticSymsTableOffset = (int)bs.Position;
            bs.WriteInt32(0x08);
            bs.WriteInt32(StaticSyms.Count);

            foreach (var symbol in StaticSyms.Values)
            {
                symbol.Write(bs);
            }
            bs.Align(0x04, grow: true);

            long lastOffset = bs.Position;
            bs.Position = baseOffset + 0x00;
            bs.WriteInt32(staticSymsTableOffset - baseOffset);

            bs.Position = lastOffset;
        }

        private void WriteFunctionLocalsSymbols(BinaryStream bs, int baseOffset)
        {
            int functionLocalsTableOffset = (int)bs.Position;
            bs.WriteInt32(0x08);
            bs.WriteInt32(FunctionLocalSymbols.Count);

            int offsetTableOffset = (int)bs.Position;
            int lastOffset = (int)bs.Position + (FunctionLocalSymbols.Count * 0x04);
            for (int i = 0; i < FunctionLocalSymbols.Count; i++)
            {
                bs.Position = offsetTableOffset + (i * 0x04);

                DebugInfoFunctionLocals funcLocals = FunctionLocalSymbols[i];
                if (funcLocals.Locals.Count == 0)
                {
                    bs.WriteInt16(0);
                    bs.WriteInt16(0);
                }
                else
                {
                    bs.WriteInt16((short)(lastOffset - offsetTableOffset));
                    bs.WriteInt16((short)funcLocals.Locals.Count);

                    bs.Position = lastOffset;
                    for (int j = 0; j <  funcLocals.Locals.Count; j++)
                        funcLocals.Locals[j].Write(bs);

                    lastOffset = (int)bs.Position;
                }
            }
            bs.Align(0x04, grow: true);
            lastOffset = (int)bs.Position;

            bs.Position = baseOffset + 0x04;
            bs.WriteInt32(functionLocalsTableOffset - baseOffset);

            bs.Position = lastOffset;
        }
    }
}
