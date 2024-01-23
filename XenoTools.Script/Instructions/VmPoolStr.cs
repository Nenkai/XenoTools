﻿using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPoolStr : VMInstructionBase
{
    public override VmInstType Type => VmInstType.POOL_STR;

    public int StringIndex { get; set; }

    public override void Read(ref SpanReader sr)
    {
        StringIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
