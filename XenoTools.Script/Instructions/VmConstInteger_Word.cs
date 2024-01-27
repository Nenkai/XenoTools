using Syroot.BinaryData;
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

    public ushort Value { get; set; }

    public VmConstInteger_Word()
    {
        
    }

    public VmConstInteger_Word(ushort value)
    {
        Value = value;
    }

    public override void Read(ref SpanReader sr)
    {
        Value = sr.ReadUInt16();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(Value);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }
}
