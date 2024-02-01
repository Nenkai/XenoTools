using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Memory;
using Syroot.BinaryData;

namespace XenoTools.Script.Entities.Debugging
{
    public class DebugInfoVariable
    {
        public short NameID { get; set; }
        public short ID { get; set; }

        public void Read(ref SpanReader sr)
        {
            NameID = sr.ReadInt16();
            sr.ReadInt16(); // Always 1
            ID = sr.ReadInt16();
            sr.ReadInt16();
            sr.ReadInt16();
        }

        public void Write(BinaryStream bs)
        {
            bs.WriteInt16(NameID);
            bs.WriteInt16(1);
            bs.WriteInt16(ID);
            bs.WriteInt16(0);
            bs.WriteInt16(0);
        }
    }
}
