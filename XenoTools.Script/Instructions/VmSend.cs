using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmSend : VMInstructionBase
{
    public override VmInstType Type => VmInstType.SEND;

    public int IDIndex { get; set; }

    public override void Read(ref SpanReader sr)
    {
        IDIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
