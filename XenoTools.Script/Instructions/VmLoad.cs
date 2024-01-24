using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoad : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD;

    public byte IDIndex { get; set; }

    public VmLoad()
    {
        
    }

    public VmLoad(byte idIndex)
    {
        IDIndex = idIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        IDIndex = sr.ReadByte();
    }

    public override void Write(ref SpanReader sr)
    {

    }
}
