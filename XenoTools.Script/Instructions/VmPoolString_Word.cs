using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolString_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_STR_W;

    public ushort StringIndex { get; set; }

    public VmPoolString_Word()
    {

    }

    public VmPoolString_Word(ushort stringIndex)
    {
        StringIndex = stringIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StringIndex = (ushort)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(StringIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - String Index: {StringIndex}";
    }
}
