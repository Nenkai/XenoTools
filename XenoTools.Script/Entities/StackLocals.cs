using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Entities
{
    public class StackLocals
    {
        public Dictionary<string, VmVariable> NamedLocals { get; set; } = [];
        public List<VmVariable> Locals { get; set; } = [];

    }
}
