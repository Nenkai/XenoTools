using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadFunction_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_FUNC_W;

    public ushort FunctionIndex { get; set; }

    public VmLoadFunction_Word()
    {

    }

    public VmLoadFunction_Word(ushort functionIndex)
    {
        FunctionIndex = functionIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FunctionIndex = (ushort)ReadValue(ref sr);
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
        return $"{Type} - Function Index: {FunctionIndex}";
    }
}
