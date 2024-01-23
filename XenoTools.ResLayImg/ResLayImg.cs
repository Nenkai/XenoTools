using System.IO;
using Syroot.BinaryData;
using System.Buffers.Binary;
using System.Net;

namespace XenoTools.Resources
{
    public class ResLayImg : ResourceObject<ResLayImg>
    {
        // ml::DevFileTh::Impl::checkValidFile

        // Instruction
        // 000000710089EA70 _ZN5layer14LayerAccFormat12setLayerDataEPKv MOV W9, #0x4448414C
        // 00000071008ACCD8 _ZN5layer15LayerObjManager15createStaticObjEPKv MOV W9, #0x4448414C
        // 00000071008AD058 _ZN5layer15LayerObjManager13createMaskObjEPKv   MOV W9, #0x4448414C
        // 00000071008AD194 _ZN5layer15LayerObjManager19createStaticMaskObjEPKv MOV W9, #0x4448414C
    }
}
