using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolString : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_STR;

    public byte StringIndex { get; set; }

    public VmPoolString()
    {

    }

    public VmPoolString(byte stringIndex)
    {
        StringIndex = stringIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StringIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(StringIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }
}
