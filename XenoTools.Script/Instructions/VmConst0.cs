﻿using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmConst0 : VMInstructionBase
{
    public override VmInstType Type => VmInstType.CONST_0;

    public override void Read(ref SpanReader sr)
    {

    }

    public override void Write(ref SpanReader sr)
    {

    }
}
