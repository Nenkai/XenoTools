using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmConstInteger : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CONST_I;

    public byte Value { get; set; }

    public VmConstInteger()
    {

    }
    
    public VmConstInteger(byte size)
    {
        Value = size;
    }

    public override void Read(ref SpanReader sr)
    {
        Value = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(Value);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }
}
