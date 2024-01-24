using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadArg : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_ARG;

    public byte ArgCount { get; set; }

    public override void Read(ref SpanReader sr)
    {
        ArgCount = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(ArgCount);
    }
}
