using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Entities;

public class FunctionInfo
{
    public short NameID { get; set; }
    public ushort NumArguments { get; set; }
    public ushort NumLocals { get; set; }
    public short LocalPoolIndex { get; set; } = -1;
    public uint CodeStartOffset { get; set; }
    public uint CodeEndOffset { get; set; }

    public int ID { get; set; }
    public List<string> Arguments { get; set; } = [];

    public static int GetSize()
    {
        return 0x14;
    }
}
