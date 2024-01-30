using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmCallIndirect : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CALL_IND;

    public byte FunctionIndex { get; set; }

    public VmCallIndirect()
    {

    }

    public override void Read(ref SpanReader sr)
    {

    }

    public override void Write(BinaryStream bs)
    {

    }

    public override int GetSize()
    {
        return 0;
    }

    public override string ToString()
    {
        return $"{Type}";
    }
}
