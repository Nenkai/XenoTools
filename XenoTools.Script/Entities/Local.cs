using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Entities;

public class VmVariable
{
    public int ID { get; set; }

    public LocalType Type { get; set; }
    public uint ArraySize { get; set; }
    public object Value { get; set; }

}

// getScTypeName
public enum LocalType
{
    Nil,
    True,
    False,
    Int,
    Fixed,
    String,
    Array,
    Function,
    Plugin,
    OC,
    Sys,
}
