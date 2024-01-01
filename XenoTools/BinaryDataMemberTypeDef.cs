using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

namespace XenoTools.BinaryData
{
    public class BinaryDataMemberTypeDef
    {
        public BinaryDataMemberType Type { get; set; }
        public BinaryDataVariableType NumericType { get; set; }
        public ushort OffsetInId { get; set; }

        public ushort ArrayLength { get; set; }

        public byte FlagShift { get; set; }
        public uint FlagMask { get; set; }
        public ushort Unk2 { get; set; }

        public void Read(SpanReader bs, long baseBdatPos)
        {
            Type = (BinaryDataMemberType)bs.ReadByte();
            if (Type == BinaryDataMemberType.Variable || Type == BinaryDataMemberType.Array)
            {
                NumericType = (BinaryDataVariableType)bs.ReadByte();
                OffsetInId = bs.ReadUInt16();
            }

            if (Type == BinaryDataMemberType.Array)
                ArrayLength = bs.ReadUInt16();
            else if (Type == BinaryDataMemberType.Flag)
            {
                FlagShift = bs.ReadByte();
                FlagMask = bs.ReadUInt32();
                Unk2 = bs.ReadUInt16();
            }
        }

        public void Write(BinaryStream bs)
        {
            bs.WriteByte((byte)Type);
            if (Type == BinaryDataMemberType.Variable || Type == BinaryDataMemberType.Array)
            {
                bs.WriteByte((byte)NumericType);
                bs.WriteUInt16(OffsetInId);
            }

            if (Type == BinaryDataMemberType.Array)
                bs.WriteUInt16(ArrayLength);
            else if (Type == BinaryDataMemberType.Flag)
            {
                bs.WriteByte(FlagShift);
                bs.WriteUInt32(FlagMask);
                bs.WriteUInt16(Unk2);
            }
        }

        public override string ToString()
        {
            return $"{Type} - {NumericType} - 0x{OffsetInId:X8}";
        }
    }

    public enum BinaryDataMemberType : ushort
    {
        Variable = 1,
        Array = 2,
        Flag = 3,
    }

    public enum BinaryDataVariableType : ushort
    {
        None,
        UByte,
        UShort,
        UInt,
        SByte,
        Short,
        Int,
        String,
        Float,
    }
}
