using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmBitwiseLeftShift : VMInstructionBase
{
    public override VmInstType Type => VmInstType.L_SHIFT;

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
}
