using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmCall : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CALL;

    public byte FunctionIndex { get; set; }

    public VmCall()
    {

    }

    public VmCall(byte functionIndex)
    {
        FunctionIndex = functionIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FunctionIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(FunctionIndex);
    }
}
