using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using XenoTools.Script.Instructions;

namespace XenoTools.Script.Entities
{
    public class LoopBlock : ControlBlock
    {
        public List<(int JumpLocation, VmJump Instruction)> ContinueJumps { get; set; } = [];
    }
}
