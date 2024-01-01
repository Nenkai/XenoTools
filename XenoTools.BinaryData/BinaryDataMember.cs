using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

namespace XenoTools.BinaryData
{
    public class BinaryDataMember
    {
        public string Name { get; set; }
        public ushort OffsetToPreviousMemberWithCollidingHash { get; set; }
        public BinaryDataMemberTypeDef TypeDef { get; set; }

        public void Read(SpanReader bs, long baseBdatPos)
        {
            ushort typeDefOffset = bs.ReadUInt16();
            OffsetToPreviousMemberWithCollidingHash = bs.ReadUInt16();
            ushort nameOffset = bs.ReadUInt16();

            bs.Position = typeDefOffset;
            TypeDef = new BinaryDataMemberTypeDef();
            TypeDef.Read(bs, baseBdatPos);

            bs.Position = nameOffset;
            Name = bs.ReadString0();
        }

        public override string ToString()
        {
            return $"{Name} - {TypeDef.Type}";
        }

        public static int GetSize()
        {
            return 0x06;
        }
    }
}
