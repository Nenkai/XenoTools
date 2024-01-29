using Syroot.BinaryData;
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

    public byte LocalIndex { get; set; }

    public VmLoad()
    {
        
    }

    public VmLoad(byte idIndex)
    {
        LocalIndex = idIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        LocalIndex = sr.ReadByte();
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(LocalIndex);
    }

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Local Index: {LocalIndex}";
    }
}
