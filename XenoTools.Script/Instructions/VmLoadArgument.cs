using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmLoadArgument : VMInstructionBase
{
    public override VmInstType Type => VmInstType.LD_ARG;

    public byte ArgumentIndex { get; set; }

    public VmLoadArgument()
    {
        
    }

    public VmLoadArgument(byte argumentIndex)
    {
        ArgumentIndex = argumentIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        ArgumentIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(ArgumentIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Argument Index: {ArgumentIndex}";
    }
}
