﻿using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmGetter : VMInstructionBase
{
    public override VmInstType Type => VmInstType.GETTER;

    public byte IDIndex { get; set; }

    public VmGetter()
    {

    }

    public VmGetter(byte idIndex)
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

    public override int GetSize()
    {
        return sizeof(byte);
    }

    public override string ToString()
    {
        return $"{Type} - Getter: {IDIndex}";
    }
}
