using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolFloat : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_FLOAT;

    public byte FloatIndex { get; set; }

    public VmPoolFloat()
    {

    }

    public VmPoolFloat(byte floatIndex)
    {
        FloatIndex = floatIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FloatIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(FloatIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Float Index: {FloatIndex}";
    }
}
