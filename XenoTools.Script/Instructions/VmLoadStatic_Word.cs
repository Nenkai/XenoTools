using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadStatic_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_STATIC_W;

    public ushort StaticIndex { get; set; }

    public VmLoadStatic_Word()
    {

    }

    public VmLoadStatic_Word(ushort staticIndex)
    {
        StaticIndex = staticIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        StaticIndex = sr.ReadUInt16();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(StaticIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - Static Index: {StaticIndex}";
    }
}
