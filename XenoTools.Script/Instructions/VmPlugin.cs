using Syroot.BinaryData;
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

    public byte PluginImportIndex { get; set; }

    public VmPlugin()
    {

    }

    public VmPlugin(byte pluginImportIndex)
    {
        PluginImportIndex = pluginImportIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        PluginImportIndex = (byte)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteByte(PluginImportIndex);
    }
}
