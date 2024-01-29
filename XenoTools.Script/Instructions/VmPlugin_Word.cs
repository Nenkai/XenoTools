using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XenoTools.Script.Instructions;

public class VmPlugin_Word : VMInstructionBase
{
    public override VmInstType Type => VmInstType.PLUGIN_W;

    public ushort PluginImportIndex { get; set; }

    public VmPlugin_Word()
    {

    }

    public VmPlugin_Word(ushort pluginImportIndex)
    {
        PluginImportIndex = pluginImportIndex;
    }

    public override void Read(ref SpanReader sr)
    {
        PluginImportIndex = (ushort)ReadValue(ref sr);
    }

    public override void Write(BinaryStream bs)
    {
        bs.WriteUInt16(PluginImportIndex);
    }

    public override int GetSize()
    {
        return sizeof(ushort);
    }

    public override string ToString()
    {
        return $"{Type} - Plugin Import Index: {PluginImportIndex}";
    }
}
