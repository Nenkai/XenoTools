using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmSetter_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.SETTER_W;

    public ushort IDIndex { get; set; }

    public VmSetter_Word()
    {

    }

    public VmSetter_Word(ushort idIndex)
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
        return $"{Type} - ID Index: {IDIndex}";
    }
}
