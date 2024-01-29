using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadFunction : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_FUNC;

    public byte FunctionIndex { get; set; }

    public VmLoadFunction()
    {

    }

    public VmLoadFunction(byte functionIndex)
    {
        FunctionIndex = functionIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FunctionIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(FunctionIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Function Index: {FunctionIndex}";
    }
}
