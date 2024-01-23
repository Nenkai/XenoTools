using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmSend_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.SEND_W;

    public int ID { get; set; }

    public override void Read(ref SpanReader sr)
    {
        ID = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
