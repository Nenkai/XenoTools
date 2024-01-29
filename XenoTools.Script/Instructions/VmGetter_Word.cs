using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmGetter_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.GETTER_W;

    public ushort IDIndex { get; set; }

    public VmGetter_Word()
    {

    }

    public VmGetter_Word(ushort idIndex)
    {
        IDIndex = idIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        IDIndex = (ushort)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(IDIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - Getter: {IDIndex}";
    }
}
