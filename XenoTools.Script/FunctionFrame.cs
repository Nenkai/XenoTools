using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script
{
    public class FunctionFrame
    {
        public List<string> Arguments { get; set; } = [];
        public List<string> Locals { get; set; } = [];
    }
}
