using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolInt : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_INT;

    public byte IntIndex { get; set; }

    public VmPoolInt()
    {

    }

    public VmPoolInt(byte index)
    {
        IntIndex = index;
    }

    public override void Read(ref SpanReader sr)
    {
        IntIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(IntIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }
}
