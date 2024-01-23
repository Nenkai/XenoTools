using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmSwitch : VMInstructionBase
{
    public override VmInstType Type => VmInstType.SWITCH;

    public byte NumBranches { get; set; }
    public uint DefaultCase { get; set; }

    public override void Read(ref SpanReader sr)
    {
        NumBranches = sr.ReadByte();
        DefaultCase = sr.ReadUInt32();

        for (int i = 0; i < NumBranches; i++)
        {
            // TODO
            sr.ReadUInt32();
            sr.ReadUInt32();
        }
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
