﻿using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolFloat_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_FLOAT_W;

    public int FloatIndex { get; set; }

    public override void Read(ref SpanReader sr)
    {
        FloatIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
