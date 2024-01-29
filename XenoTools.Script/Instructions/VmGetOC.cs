using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmGetOC : VMInstructionBase
{
    public override VmInstType Type => VmInstType.GET_OC;

    public byte OCIndex { get; set; }

    public VmGetOC()
    {

    }

    public VmGetOC(byte ocIndex)
    {
        OCIndex = ocIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        OCIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(OCIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - OC Index: {OCIndex}";
    }
}
