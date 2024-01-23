using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolInt_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_INT_W;

    public int IntIndex { get; set; }

    public VmPoolInt_Word()
    {

    }

    public VmPoolInt_Word(short index)
    {
        IntIndex = index;
    }

    public override void Read(ref SpanReader sr)
    {
        IntIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
