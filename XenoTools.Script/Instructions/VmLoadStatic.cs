using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadStatic : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_STATIC;

    public byte StaticIndex { get; set; }

    public VmLoadStatic()
    {

    }

    public VmLoadStatic(byte staticIndex)
    {
        StaticIndex = staticIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StaticIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(StaticIndex);
    }
}
