using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmStore : VMInstructionBase
{
    public override VmInstType Type => VmInstType.ST;

    public byte LocalIndex { get; set; }

    public VmStore()
    {

    }

    public VmStore(byte stackIndex)
    {
        LocalIndex = stackIndex;
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
