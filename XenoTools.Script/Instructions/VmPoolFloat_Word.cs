using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolFloat_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_FLOAT_W;

    public ushort FloatIndex { get; set; }

    public VmPoolFloat_Word()
    {

    }

    public VmPoolFloat_Word(ushort floatIndex)
    {
        FloatIndex = floatIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        FloatIndex = (ushort)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(FloatIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - Float Index: {FloatIndex}";
    }
}
