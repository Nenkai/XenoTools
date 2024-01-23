﻿using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmGetter : VMInstructionBase
{
    public override VmInstType Type => VmInstType.GETTER;

    public int IDIndex { get; set; }

    public override void Read(ref SpanReader sr)
    {
        IDIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {

    }
}