using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmSetter : VMInstructionBase
{
    public override VmInstType Type => VmInstType.SETTER;

    public byte IDIndex { get; set; }

    public VmSetter()
    {

    }

    public VmSetter(byte idIndex)
    {
        IDIndex = idIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        IDIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(IDIndex);
    }
}
