using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmStore : VMInstructionBase
{
    public override VmInstType Type => VmInstType.ST;

    public byte StackIndex { get; set; }

    public VmStore()
    {

    }

    public VmStore(byte stackIndex)
    {
        StackIndex = stackIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StackIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(StackIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }
}
