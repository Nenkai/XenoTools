using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmConstInteger_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CONST_I_W;

    public short Value { get; set; }

    public VmConstInteger_Word()
    {
        
    }

    public VmConstInteger_Word(short value)
    {
        Value = value;
    }

    public override void Read(ref SpanReader sr)
    {
        Value = sr.ReadInt16();
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
