using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmCall_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CALL_W;

    public ushort FunctionIndex { get; set; }

    public VmCall_Word()
    {

    }

    public VmCall_Word(ushort functionIndex)
    {
        FunctionIndex = functionIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FunctionIndex = sr.ReadUInt16();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(FunctionIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - Func Index: {FunctionIndex}";
    }
}
