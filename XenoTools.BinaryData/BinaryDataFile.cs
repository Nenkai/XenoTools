using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Buffers.Binary;
using System.Diagnostics;

using Syroot.BinaryData;
using Syroot.BinaryData.Memory;

using XenoTools.BinaryData.ID;

namespace XenoTools.BinaryData;

public class BinaryDataFile
{
    /// <summary>
    /// 'BDAT'
    /// </summary>
    public const uint MAGIC = 0x54414442;

    private Memory<byte> _file;
    private long _basePos;

    public string Name { get; set; }
    public ushort Flags { get; set; }
    public ushort Checksum { get; set; }
    public ushort IdTop { get; set; }
    public ushort[] MemberNameHashLookupTable { get; set; }

    /// <summary>
    /// IDs - or, rows
    /// </summary>
    public List<byte[]> IDs { get; set; } = new();
    public List<BinaryDataMember> Members { get; set; } = new();

    /// <summary>
    /// Reads the binary data.
    /// </summary>
    /// <param name="data"></param>
    /// <exception cref="InvalidDataException"></exception>
    /// <exception cref="Exception"></exception>
    // Non original, but could be useful
    public void Read(Memory<byte> data)
    {
        _file = data;

        SpanReader sr = new SpanReader(data.Span);
        _basePos = sr.Position;

        if (sr.ReadInt32() != 0x54414442)
            throw new InvalidDataException();

        Flags = sr.ReadUInt16();
        ushort memberNamesOffset = sr.ReadUInt16();
        ushort idSize = sr.ReadUInt16();
        ushort memberNamesHashLookupTableOffset = sr.ReadUInt16();
        ushort memberNamesHashLookupCount = sr.ReadUInt16();
        ushort idsOffset = sr.ReadUInt16();
        ushort idsCount = sr.ReadUInt16();
        IdTop = sr.ReadUInt16();
        ushort unkVersionMaybe = sr.ReadUInt16();
        Checksum = sr.ReadUInt16();
        uint stringsOffset = sr.ReadUInt32();
        uint totalStringLength = sr.ReadUInt32();
        ushort membersOffset = sr.ReadUInt16();
        ushort memberCount = sr.ReadUInt16();


        if ((Flags & 0x02) != 0)
        {
            // decodeScramble
            if (totalStringLength != 0)
                DecodeScrambleSub(_file.Span.Slice((int)stringsOffset), Checksum, totalStringLength);

            DecodeScrambleSub(_file.Span.Slice(memberNamesOffset), Checksum, (uint)(memberNamesHashLookupTableOffset - memberNamesOffset));
        }

        MemberNameHashLookupTable = new ushort[memberNamesHashLookupCount];
        sr.Position = memberNamesHashLookupTableOffset;
        for (int i = 0; i < memberNamesHashLookupCount; i++)
            MemberNameHashLookupTable[i] = sr.ReadUInt16();

        for (int i = 0; i < memberCount; i++)
        {
            sr.Position = membersOffset + i * BinaryDataMember.GetSize();
            var member = new BinaryDataMember();
            member.Read(sr, _basePos);
            Members.Add(member);
        }

        for (int i = 0; i < idsCount; i++)
        {
            sr.Position = idsOffset + i * idSize;
            byte[] idData = sr.ReadBytes(idSize);
            IDs.Add(idData);
        }

        sr.Position = memberNamesOffset;
        Name = sr.ReadString0();

        if (!CalcCheckSumSub())
            throw new Exception($"Bdat file {Name}'s checksum did not match");
    }

    // Bdat::calcCheckSumSub - 0x71005AFDE0
    public bool CalcCheckSumSub()
    {
        SpanReader sr = new SpanReader(_file.Span);
        sr.Position = 0x18;
        int stringsOffset = sr.ReadInt32();
        int totalStringLength = sr.ReadInt32();

        const int StartChecksumOffset = 0x20;

        int endFileOffset = stringsOffset + totalStringLength;
        if (endFileOffset < 0x21)
            return false;

        int numExtraByte = endFileOffset & 1;
        int lastByteOffset;
        ushort chk = 0;
        if (endFileOffset == 0x21)
            lastByteOffset = 0x20;
        else
        {
            int sizeToCheck = endFileOffset - 0x20 - numExtraByte;
            for (int i = 0; i < sizeToCheck; i += 2)
            {
                sr.Position = (int)_basePos + StartChecksumOffset + i;
                int b1 = sr.ReadByte();

                sr.Position = (int)_basePos + StartChecksumOffset + i + 1;
                int b2 = sr.ReadByte();

                chk += (byte)(b1 << (i + 32 & 2));
                chk += (byte)(b2 << (i + 33 & 3));
            }

            lastByteOffset = sizeToCheck + 0x20;
            if (numExtraByte == 0)
                return chk == Checksum;
        }

        sr.Position = lastByteOffset;
        chk += (byte)(sr.ReadByte() << (lastByteOffset & 3));
        return chk == Checksum;
    }

    /// <summary>
    /// Gets the 'sheet'/file name for this file.
    /// </summary>
    /// <returns></returns>
    // Bdat::getSheetName - 0x71005B0240
    public string GetSheetName()
    {
        return Name;
    }

    /// <summary>
    /// Gets a member by name.
    /// </summary>
    /// <param name="memberName"></param>
    /// <returns></returns>
    // Bdat::getMember - 0x71005B0250
    public BinaryDataMember GetMember(string memberName)
    {
        int lookupIdx = CalcHash(memberName, MemberNameHashLookupTable.Length);
        int memberOffset = MemberNameHashLookupTable[lookupIdx];

        SpanReader sr = new SpanReader(_file.Span);
        sr.Position = memberOffset;

        BinaryDataMember member = new BinaryDataMember();
        member.Read(sr, _basePos);

        while (true)
        {
            if (member.Name == memberName)
                return member;

            if (member.OffsetToPreviousMemberWithCollidingHash == 0)
                return null;

            sr.Position = member.OffsetToPreviousMemberWithCollidingHash;
            member = new BinaryDataMember();
            member.Read(sr, _basePos);
        }
    }

    /// <summary>
    /// Gets a member by member index.
    /// </summary>
    /// <param name="memberIndex"></param>
    /// <returns></returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    // Bdat::getMember - 0x71005B04D0
    public BinaryDataMember GetMember(int memberIndex)
    {
        if (memberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        return Members[memberIndex];
    }

    /// <summary>
    /// Gets the number of members in this file.
    /// </summary>
    /// <returns></returns>
    // Bdat::getMemberSize - 0x71005B0520
    public int GetMemberSize()
    {
        return Members.Count;
    }

    /// <summary>
    /// Gets the name of a member by member index.
    /// </summary>
    /// <param name="memberIndex"></param>
    /// <returns></returns>
    // Bdat::getMemberName - 0x71005B0530
    public string GetMemberName(int memberIndex)
    {
        if (memberIndex > Members.Count - 1)
            return null;

        return Members[memberIndex].Name;
    }

    /// <summary>
    /// Gets the value of a member within an id.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    // Bdat::getVal - 0x71005B0570
    public object GetVal(string memberName, int id)
    {
        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return null;

        return GetVal(member, id);
    }

    /// <summary>
    /// Gets/calculates the id index of an actual id.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    // Bdat::calcId - 0x71005B08D0
    public bool CalcId(out int index, int id)
    {
        index = 0;
        int idIndex = id - IdTop;
        if (idIndex >= IDs.Count)
            return false;

        index = idIndex;
        return true;
    }

    /// <summary>
    /// Gets the value of a member within an id.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    // Bdat::getVal - 0x71005B07A0
    private object GetVal(BinaryDataMember member, int id)
    {
        int idIndex = id - IdTop;
        if (idIndex >= IDs.Count)
            return null;

        return GetVar(member, idIndex);
    }

    /// <summary>
    /// Gets the variable of a member by id index.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    // Bdat::getVar - 0x71005B0900
    public object GetVar(BinaryDataMember member, int idIndex)
    {
        if (member.TypeDef.Type != BinaryDataMemberType.Variable)
            return null;

        Span<byte> idData = IDs[idIndex].AsSpan(member.TypeDef.OffsetInId);
        return GetVarVal(idData, member.TypeDef.NumericType);
    }

    /// <summary>
    /// Gets the value of a variable by member index and id.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    // Bdat::getVal - 0x71005B09A0
    public object GetVal(int memberIndex, int id)
    {
        if (memberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        return GetVal(Members[memberIndex], id);
    }

    /// <summary>
    /// Gets a array value by member name, id and array index.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <param name="arrayIndex"></param>
    /// <returns></returns>
    // Bdat::getArrayVal - 0x71005B09F0
    public object GetArrayVal(string memberName, int id, int arrayIndex)
    {
        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return null;

        return GetArrayVal(member, id, arrayIndex);
    }

    /// <summary>
    /// Gets a array value by member, id and array index.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="id"></param>
    /// <param name="arrayIndex"></param>
    /// <returns></returns>
    // Bdat::getArrayVal - 0x71005B0C50
    public object GetArrayVal(BinaryDataMember member, int id, int arrayIndex)
    {
        int idIndex = id - IdTop;
        if (idIndex >= IDs.Count)
            return null;

        if (member.TypeDef.Type != BinaryDataMemberType.Array || member.TypeDef.ArrayLength <= arrayIndex)
            return null;

        int typeSize = GetNumericTypeSize(member.TypeDef.NumericType);
        Span<byte> idData = IDs[idIndex].AsSpan(member.TypeDef.OffsetInId + arrayIndex * typeSize);

        return GetVarVal(idData, member.TypeDef.NumericType);
    }


    // Bdat::getArray - 0x71005B0DE0
    public object GetArray(BinaryDataMember member, int idIndex, int arrayIndex)
    {
        if (member.TypeDef.Type != BinaryDataMemberType.Array)
            return null;

        if (member.TypeDef.ArrayLength <= arrayIndex)
            return null;

        int typeSize = GetNumericTypeSize(member.TypeDef.NumericType);
        Span<byte> idData = IDs[idIndex].AsSpan(member.TypeDef.OffsetInId + arrayIndex * typeSize);

        return GetVarVal(idData, member.TypeDef.NumericType);
    }


    // Bdat::getArrayVal - 0x71005B0ED0
    public object GetArrayVal(int memberIndex, int id, int arrayIndex)
    {
        if (memberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        return GetArrayVal(Members[memberIndex], id, arrayIndex);
    }

    // Bdat::getArrayCount - 0x71005B0F20
    public int GetArrayCount(string memberName)
    {
        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return 0;

        return GetArrayCount(member);
    }

    // Bdat::getArrayCount - 0x71005B10E0
    public int GetArrayCount(BinaryDataMember member)
    {
        if (member.TypeDef.Type == BinaryDataMemberType.Array)
            return member.TypeDef.ArrayLength;
        else
            return 0;
    }

    // Bdat::getArrayCount - 0x71005B1190
    public int GetArrayCount(int memberIndex)
    {
        if (memberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        var member = Members[memberIndex];
        return GetArrayCount(member);
    }

    /// <summary>
    /// Gets the number of ids in the file.
    /// </summary>
    /// <returns></returns>
    // Bdat::getIdCount - 0x71005B1230
    public int GetIdCount()
    {
        return (ushort)IDs.Count;
    }

    /// <summary>
    /// Gets the first id in the file.
    /// </summary>
    /// <returns></returns>
    // Bdat::getIdTop - 0x71005B1270
    public int GetIdTop()
    {
        return IdTop;
    }

    /// <summary>
    /// Gets the last id in the file.
    /// </summary>
    /// <returns></returns>
    // Bdat::getIdEnd - 0x71005B12B0
    public int GetIdEnd()
    {
        return IdTop - IDs.Count - 1;
    }

    /// <summary>
    /// Gets the type of a member by name.
    /// </summary>
    /// <param name="memberName"></param>
    /// <returns></returns>
    // Bdat::getVarType - 0x71005B12F0
    public BinaryDataMemberType GetVarType(string memberName)
    {
        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return 0;

        return GetVarType(member);
    }

    /// <summary>
    /// Gets the variable type of a member.
    /// </summary>
    /// <param name="member"></param>
    /// <returns></returns>
    // Bdat::getVarType - 0x71005B1490
    public BinaryDataMemberType GetVarType(BinaryDataMember member)
    {
        return member.TypeDef.Type;
    }

    /// <summary>
    /// Gets the variable type of a member by index.
    /// </summary>
    /// <param name="memberIndex"></param>
    /// <returns></returns>
    /// <exception cref="IndexOutOfRangeException"></exception>
    // Bdat::getVarType - 0x71005B1510
    public BinaryDataMemberType GetVarType(int memberIndex)
    {
        if (memberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        var member = Members[memberIndex];
        return member.TypeDef.Type;
    }

    // Bdat::getFlagVal - 0x71005B1590
    public object GetFlagVal(string memberName, int id, string flagName)
    {
        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return null;

        BinaryDataMember flagMember = GetMember(flagName);
        if (flagMember is null)
            return null;

        return GetFlagVal(member, id, flagMember);
    }

    // Bdat::getFlagVal - 0x71005B1870
    public object GetFlagVal(BinaryDataMember member, int id, BinaryDataMember flagMember)
    {
        int idIndex = id - IdTop;
        if (idIndex >= IDs.Count)
            return null;

        if (flagMember.TypeDef.Type != BinaryDataMemberType.Flag)
            return null;

        // TODO
        return null;
    }

    // Bdat::getFlagVal - 0x71005B1B30
    public object GetFlagVal(int memberIndex, int id, int flagMemberIndex)
    {
        if (memberIndex > Members.Count - 1 || flagMemberIndex > Members.Count - 1)
            throw new IndexOutOfRangeException("Member index out of bounds");

        return GetFlagVal(Members[memberIndex], id, Members[flagMemberIndex]);
    }

    /// <summary>
    /// Gets the shift and mask for a member & a flag.
    /// </summary>
    /// <param name="memberName"></param>
    /// <param name="flagName"></param>
    /// <param name="shift"></param>
    /// <param name="mask"></param>
    /// <returns></returns>
    // Bdat::getShiftMask - 0x71005B1BA0
    public bool GetShiftMask(string memberName, string flagName, out byte shift, out uint mask)
    {
        shift = 0;
        mask = 0;

        BinaryDataMember member = GetMember(memberName);
        if (member is null)
            return false;

        BinaryDataMember flagMember = GetMember(flagName);
        if (flagMember is null)
            return false;

        if (flagMember.TypeDef.Type == BinaryDataMemberType.Flag)
        {
            shift = flagMember.TypeDef.FlagShift;
            mask = flagMember.TypeDef.FlagMask;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Calculates the hash of a string.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="len"></param>
    /// <returns></returns>
    // Bdat::calcHash - 0x71005B03D0
    public static int CalcHash(string value, int len)
    {
        int hash = 0;
        float minLen = Math.Min(value.Length, 8);

        for (int i = 0; i < minLen; i++)
            hash = value[i] + 7 * hash;

        return hash % len;
    }

    /// <summary>
    /// De-scrambles data.
    /// </summary>
    /// <param name="bs"></param>
    /// <param name="checksum"></param>
    /// <param name="length"></param>
    // Bdat::decodeScrambleSub - 0x71005B2E80
    private static void DecodeScrambleSub(Span<byte> data, ushort checksum, /* uint start, */ uint length)
    {
        byte chk_hi = (byte)(~checksum >> 8);
        byte chk_lo = (byte)~checksum;

        for (int j = 0; j < length; j += 2)
        {
            byte hi = data[j];
            byte lo = data[j + 1];

            data[j] = (byte)(hi ^ chk_hi);
            data[j + 1] = (byte)(lo ^ chk_lo);

            chk_hi += hi;
            chk_lo += lo;
        }
    }

    /// <summary>
    /// Gets the variable value within id data.
    /// </summary>
    /// <param name="idData"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    // Bdat::getVarVal - 0x71005B2DE0
    private object GetVarVal(Span<byte> idData, BinaryDataVariableType type)
    {
        switch (type)
        {
            case BinaryDataVariableType.UByte:
                return idData[0];

            case BinaryDataVariableType.SByte:
                return (sbyte)idData[0];

            case BinaryDataVariableType.UShort:
                return BinaryPrimitives.ReadUInt16LittleEndian(idData);

            case BinaryDataVariableType.Short:
                return BinaryPrimitives.ReadInt16LittleEndian(idData);

            case BinaryDataVariableType.UInt:
                return BinaryPrimitives.ReadUInt32LittleEndian(idData);

            case BinaryDataVariableType.Int:
                return BinaryPrimitives.ReadInt32LittleEndian(idData);

            case BinaryDataVariableType.Float:
                return BinaryPrimitives.ReadSingleLittleEndian(idData);

            case BinaryDataVariableType.String:
                int strOffset = BinaryPrimitives.ReadInt32LittleEndian(idData);
                SpanReader sr = new SpanReader(_file.Span[strOffset..]);
                return sr.ReadString0();

            default:
                throw new NotImplementedException();
        }
    }

    private static int GetNumericTypeSize(BinaryDataVariableType numericType)
    {
        return numericType switch
        {
            BinaryDataVariableType.UByte => 1,
            BinaryDataVariableType.SByte => 1,
            BinaryDataVariableType.UShort => 2,
            BinaryDataVariableType.Short => 2,
            BinaryDataVariableType.UInt => 4,
            BinaryDataVariableType.Int => 4,
            BinaryDataVariableType.Float => 4,
            BinaryDataVariableType.String => throw new NotSupportedException(),
            _ => throw new NotSupportedException()
        };
    }

    /* ************************************
     * 
     * NON ORIGINAL IMPLEMENTATIONS 
     * 
     **************************************/

    /// <summary>
    /// Serializes the file.
    /// </summary>
    /// <param name="bs"></param>
    public void Write(BinaryStream bs)
    {
        var bdatFileLayoutAndID = GetLayoutAndIDs();
        var writer = new BinaryDataFileWriter(Name, IdTop, bdatFileLayoutAndID.Layout, bdatFileLayoutAndID.IDs);
        writer.Write(bs);
    }

    // Non original, but an utility to grab every id and linked data together
    public (List<BinaryDataIDMember> Layout, List<BinaryDataID> IDs) GetLayoutAndIDs()
    {
        int idTop = GetIdTop();
        int idCount = GetIdCount();

        List<BinaryDataIDMember> members = new List<BinaryDataIDMember>();
        for (int i = 0; i< Members.Count; i++)
        {
            BinaryDataMember bdatFileMember = Members[i];
            
            var idMember = new BinaryDataIDMember();
            idMember.Name = bdatFileMember.Name;
            idMember.Type = bdatFileMember.TypeDef.Type;
            idMember.NumericType = bdatFileMember.TypeDef.NumericType;
            idMember.ArrayLength = bdatFileMember.TypeDef.ArrayLength;
            members.Add(idMember);
        }

        List<BinaryDataID> ids = new List<BinaryDataID>();
        for (int idIdx = 0; idIdx < idCount; idIdx++)
        {
            BinaryDataID id = new BinaryDataID();
            id.IDNumber = IdTop + idIdx;

            int numMembers = GetMemberSize();
            for (int memberIndex = 0; memberIndex < numMembers; memberIndex++)
            {
                BinaryDataMember member = GetMember(memberIndex);

                //Console.WriteLine($"- {member}");

                if (member.TypeDef.Type == BinaryDataMemberType.Variable)
                {
                    object val = GetVar(member, idIdx);
                    id.Values.Add(new BinaryDataVariable(member.Name, member.TypeDef.Type, member.TypeDef.NumericType, val));
                }
                else if (member.TypeDef.Type == BinaryDataMemberType.Array)
                {
                    List<object> arr = new List<object>();
                    for (int k = 0; k < member.TypeDef.ArrayLength; k++)
                    {
                        object val = GetArray(member, idIdx, k);
                        arr.Add(val);
                    }

                    id.Values.Add(new BinaryDataArray(member.Name, member.TypeDef.Type, member.TypeDef.NumericType, arr));
                }
                else if (member.TypeDef.Type == BinaryDataMemberType.Flag)
                {
                    ;
                }
            }

            ids.Add(id);
        }

        return (members, ids);
    }
}
