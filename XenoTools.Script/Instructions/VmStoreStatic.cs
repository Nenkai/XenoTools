using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmStoreStatic : VMInstructionBase
{
    public override VmInstType Type => VmInstType.ST_STATIC;

    public byte StaticIndex { get; set; }

    public VmStoreStatic()
    {

    }

    public VmStoreStatic(byte staticIndex)
    {
        StaticIndex = staticIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StaticIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(StaticIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Static Index: {StaticIndex}";
    }
}
