using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmGetOC_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.GET_OC_W;

    public ushort OCIndex { get; set; }

    public VmGetOC_Word()
    {

    }

    public VmGetOC_Word(ushort ocIndex)
    {
        OCIndex = ocIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        OCIndex = (ushort)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(OCIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - OC Index: {OCIndex}";
    }
}
