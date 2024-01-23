using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPlugin : VMInstructionBase
{
    public override VmInstType Type => VmInstType.PLUGIN;

    public int PluginImportIndex { get; set; }

    public override void Read(ref SpanReader sr)
    {
        PluginImportIndex = ReadValue(ref sr);
    }

    public override void Write(ref SpanReader sr)
    {
        throw new NotImplementedException();
    }
}
