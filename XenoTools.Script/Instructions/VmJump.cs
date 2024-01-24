using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmJump : VMInstructionBase
{
    public override VmInstType Type => VmInstType.JMP;

    public ushort JumpRelativeOffset;

    public override void Read(ref SpanReader sr)
    {
        JumpRelativeOffset = sr.ReadUInt16();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(JumpRelativeOffset);
    }
}
