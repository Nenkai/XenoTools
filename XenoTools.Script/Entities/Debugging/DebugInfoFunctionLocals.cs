using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

namespace XenoTools.Script.Entities.Debugging
{
    public class DebugInfoFunctionLocals
    {
        public Dictionary<int, DebugInfoVariable> Locals { get; set; } = [];

    }
}
