using Syroot.BinaryData;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

using XenoTools.Utils;
using XenoTools.BinaryData.ID;

namespace XenoTools.BinaryData
{
    public class BinaryDataFileWriter
    {
        private List<BinaryDataID> IDs;
        private uint _idSize;

        public string _name { get; set; }
        private ushort _idTop { get; set; }
        private List<BinaryDataMember> _members = new();

        public const ushort LOOKUP_TABLE_DEFAULT_SIZE = 61;
        private ushort _lookupTableSize = LOOKUP_TABLE_DEFAULT_SIZE;

        private SortedDictionary<int, List<BinaryDataMember>> _hashToMemberList = new(); // Bound to collide, keep a list of each member for each hash

        private Dictionary<string, uint> _stringOffsets = new();
        private long _currentStringOffset;

        public BinaryDataFileWriter(string name, ushort idTop, List<BinaryDataID> ids)
        {
            _name = name;
            _idTop = idTop;
            IDs = ids;
        }

        private void BuildMembers()
        {
            if (IDs.Count <= 0)
                return;

            _idSize = MiscUtils.AlignValue(IDs[0].GetSize(), 0x04);

            ushort offset = 0;

            for (int i = 0; i < IDs[0].Values.Count; i++)
            {
                BinaryDataValue value = IDs[0].Values[i];

                BinaryDataMember dataMember = new BinaryDataMember();
                dataMember.Name = value.Name;

                BinaryDataMemberTypeDef typeDef = new BinaryDataMemberTypeDef();
                typeDef.Type = value.Type;

                if (typeDef.Type == BinaryDataMemberType.Variable)
                {
                    BinaryDataVariable variable = value as BinaryDataVariable;
                    typeDef.NumericType = variable.NumericType;
                    typeDef.OffsetInId = offset;
                    offset += (ushort)value.GetSize();
                }
                else if (typeDef.Type == BinaryDataMemberType.Array)
                {
                    BinaryDataArray variable = value as BinaryDataArray;
                    typeDef.NumericType = variable.NumericType;
                    typeDef.ArrayLength = (ushort)variable.Values.Count;
                    typeDef.OffsetInId = offset;
                    offset += (ushort)(value.GetSize() * variable.Values.Count);
                }
                else if (typeDef.Type == BinaryDataMemberType.Flag)
                {
                    throw new NotImplementedException();
                }

                dataMember.TypeDef = typeDef;

                _members.Add(dataMember);
            }
        }

        private void VerifyIntegrity()
        {
            throw new NotImplementedException();
        }

        public void Write(BinaryStream bs)
        {
            // Step 0: Build the list of members
            BuildMembers();

            long baseFilePos = bs.Position;
            bs.Position += 0x40; // Header 0x40, skip as we write it at the end

            // Step 1: Write member types
            Dictionary<BinaryDataMemberTypeDef, uint> MemberTypeToOffset = new Dictionary<BinaryDataMemberTypeDef, uint>();
            for (int i = 0; i < _members.Count; i++)
            {
                BinaryDataMember member = _members[i];
                MemberTypeToOffset.Add(member.TypeDef, (uint)(bs.Position - baseFilePos));

                member.TypeDef.Write(bs);
                bs.Align(0x02, grow: true);
            }

            // Step 2: Write member names
            long sheetNameOffset = bs.Position - baseFilePos;
            bs.WriteString(_name, StringCoding.ZeroTerminated); // First one is always file name
            bs.Align(0x02, grow: true);

            Dictionary<BinaryDataMember, uint> MemberToNameOffset = new Dictionary<BinaryDataMember, uint>();
            for (int i = 0; i < _members.Count; i++)
            {
                MemberToNameOffset.Add(_members[i], (uint)(bs.Position - baseFilePos));

                bs.WriteString(_members[i].Name, StringCoding.ZeroTerminated);
                bs.Align(0x02, grow: true);
            }

            // Step 3: Write members (& create lookup table)
            long membersOffset = bs.Position - baseFilePos;
            Dictionary<BinaryDataMember, uint> MemberToOffset = new Dictionary<BinaryDataMember, uint>();
            for (int i = 0; i < _members.Count; i++)
            {
                BinaryDataMember member = _members[i];
                MemberToOffset.Add(member, (uint)(bs.Position - baseFilePos));

                int nameHash = BinaryDataFile.CalcHash(member.Name, _lookupTableSize);

                uint typeDefOffset = MemberTypeToOffset[member.TypeDef];
                Debug.Assert(typeDefOffset < ushort.MaxValue, "Offset to member type def too large");

                uint memberNameOffset = MemberToNameOffset[member];
                Debug.Assert(typeDefOffset < ushort.MaxValue, "Offset to member name too large");

                uint offsetToPreviousMemberWithCollidingHash = 0; // None yet
                if (_hashToMemberList.TryGetValue(nameHash, out List<BinaryDataMember> collidingNameMembers))
                {
                    BinaryDataMember last = collidingNameMembers[^1];
                    offsetToPreviousMemberWithCollidingHash = MemberToOffset[last];
                }
                Debug.Assert(offsetToPreviousMemberWithCollidingHash < ushort.MaxValue, "Offset to previous colliding member hash too large");

                bs.WriteUInt16((ushort)typeDefOffset);
                bs.WriteUInt16((ushort)offsetToPreviousMemberWithCollidingHash);
                bs.WriteUInt16((ushort)memberNameOffset);

                if (!_hashToMemberList.TryGetValue(nameHash, out List<BinaryDataMember> memberList))
                    _hashToMemberList.Add(nameHash, new List<BinaryDataMember>() { member });
                else
                    memberList.Add(member);
            }

            // Step 4: Member hash lookup table
            long lookupHashesTableOffset = bs.Position - baseFilePos;
            for (int i = 0; i < _lookupTableSize; i++)
            {
                if (_hashToMemberList.TryGetValue(i, out List<BinaryDataMember> collidingNameMembers))
                {
                    BinaryDataMember last = collidingNameMembers[^1];
                    ushort offset = (ushort)MemberToOffset[last];
                    bs.WriteUInt16(offset);
                }
                else
                    bs.WriteUInt16(0);
            }
            bs.Align(0x10, grow: true);

            // Step 5: IDs (& string table which appear after the ids)
            long idsOffset = bs.Position - baseFilePos;
            long stringsOffset = idsOffset + _idSize * IDs.Count;
            stringsOffset = MiscUtils.AlignValue((uint)stringsOffset, 0x10);

            _currentStringOffset = stringsOffset;

            for (int i = 0; i < IDs.Count; i++)
            {
                BinaryDataID id = IDs[i];
                bs.Position = baseFilePos + idsOffset + i * _idSize;

                for (int j = 0; j < id.Values.Count; j++)
                {
                    BinaryDataValue value = id.Values[j];
                    if (value.Type == BinaryDataMemberType.Variable)
                    {
                        BinaryDataVariable var = (BinaryDataVariable)value;
                        WriteVariable(bs, var.NumericType, var.Value, baseFilePos);
                    }
                    else if (value.Type == BinaryDataMemberType.Array)
                    {
                        BinaryDataArray arr = (BinaryDataArray)value;
                        for (int k = 0; k < arr.Values.Count; k++)
                            WriteVariable(bs, arr.NumericType, arr.Values[k], baseFilePos);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            // Step 6: Align id string table (total size includes padding)
            bs.Position = baseFilePos + _currentStringOffset;
            long totalStringLength = bs.Position - baseFilePos - stringsOffset;
            bs.Align(0x10, grow: true);

            long lastPos = bs.Position;

            // Step 7: Member fields, required for checksum computation
            bs.Position = baseFilePos + 0x20;
            bs.WriteUInt16((ushort)membersOffset);
            bs.WriteUInt16((ushort)_members.Count);

            // Step 8: Compute checksum. Computed starting from 0x20
            ushort checksum = GetChecksum(bs, baseFilePos, (int)stringsOffset, (int)totalStringLength);

            // Step 9: Header =)
            bs.Position = baseFilePos;
            bs.WriteUInt32(BinaryDataFile.MAGIC);
            bs.WriteUInt16(0);
            bs.WriteUInt16((ushort)sheetNameOffset);
            bs.WriteUInt16((ushort)_idSize);
            bs.WriteUInt16((ushort)lookupHashesTableOffset);
            bs.WriteUInt16(_lookupTableSize);
            bs.WriteUInt16((ushort)idsOffset);
            bs.WriteUInt16((ushort)IDs.Count);
            bs.WriteUInt16(_idTop);
            bs.WriteUInt16(2); // Unknown
            bs.WriteUInt16(checksum); // Checksum (writen after)
            bs.WriteUInt32((uint)stringsOffset); // Strings offset
            bs.WriteUInt32((uint)totalStringLength); // Total String Length
            // membersOffset
            // memberCount

            // Done, owo.
            // Point to end of file.
            bs.Position = lastPos;
        }

        private ushort GetChecksum(BinaryStream bs, long baseFilePos, int stringsOffset, int totalStringLength)
        {
            const int StartChecksumOffset = 0x20;

            int endFileOffset = stringsOffset + totalStringLength;
            if (endFileOffset < 0x21)
                return 0;

            int numExtraByte = endFileOffset & 1;
            int lastByteOffset;

            ushort chk = 0;
            if (endFileOffset == 0x21)
            {
                lastByteOffset = 0x20;
            }
            else
            {
                int sizeToCheck = endFileOffset - 0x20 - numExtraByte;
                for (int i = 0; i < sizeToCheck; i += 2)
                {
                    bs.Position = (int)baseFilePos + StartChecksumOffset + i;
                    int b1 = bs.ReadByte();

                    bs.Position = (int)baseFilePos + StartChecksumOffset + i + 1;
                    int b2 = bs.ReadByte();

                    chk += (byte)(b1 << (i + 32 & 2));
                    chk += (byte)(b2 << (i + 33 & 3));
                }

                lastByteOffset = sizeToCheck + 0x20;
                if (numExtraByte == 0)
                    return chk;
            }

            bs.Position = lastByteOffset;
            chk += (byte)(bs.ReadByte() << (lastByteOffset & 3));
            return chk;
        }

        private void WriteVariable(BinaryStream bs, BinaryDataVariableType numType, object val, long baseFilePos)
        {
            switch (numType)
            {
                case BinaryDataVariableType.UByte:
                    bs.WriteByte((byte)val);
                    break;

                case BinaryDataVariableType.UShort:
                    bs.WriteUInt16((ushort)val);
                    break;

                case BinaryDataVariableType.UInt:
                    bs.WriteUInt32((uint)val);
                    break;

                case BinaryDataVariableType.SByte:
                    bs.WriteSByte((sbyte)val);
                    break;

                case BinaryDataVariableType.Short:
                    bs.WriteInt16((short)val);
                    break;

                case BinaryDataVariableType.Int:
                    bs.WriteInt32((int)val);
                    break;

                case BinaryDataVariableType.String:
                    string str = (string)val;
                    if (_stringOffsets.TryGetValue(str, out uint offset))
                    {
                        bs.WriteUInt32(offset);
                    }
                    else
                    {
                        long tmp = bs.Position;

                        bs.Position = baseFilePos + _currentStringOffset;
                        bs.WriteString(str, StringCoding.ZeroTerminated);
                        bs.Align(0x02, grow: true);

                        _stringOffsets.Add(str, (uint)_currentStringOffset);
                        _currentStringOffset = bs.Position - baseFilePos;

                        bs.Position = tmp;
                        bs.WriteUInt32(_stringOffsets[str]);
                    }
                    break;

                case BinaryDataVariableType.Float:
                    bs.WriteSingle((float)val);
                    break;

                default:
                    break;
            }
        }
    }
}
