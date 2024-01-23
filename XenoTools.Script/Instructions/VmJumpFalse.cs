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

    public ushort JumpRelativeOffset;

    public override void Read(ref SpanReader sr)
    {
        JumpRelativeOffset = sr.ReadUInt16();
    }

    public override void Write(ref SpanReader sr)
    {
        
    }
}
