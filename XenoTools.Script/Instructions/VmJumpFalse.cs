using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmJumpFalse : VMInstructionBase
{
    public override VmInstType Type => VmInstType.JPF;

    public short JumpRelativeOffset;

    public override void Read(ref SpanReader sr)
    {
        JumpRelativeOffset = sr.ReadInt16();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteInt16(JumpRelativeOffset);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }
}
