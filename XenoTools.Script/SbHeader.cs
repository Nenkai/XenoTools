using Syroot.BinaryData.Memory;

namespace XenoTools.Script;

public class SbHeader
{
    public uint Magic { get; set; }
    public byte Version { get; set; }
    public byte A;
    public byte B;
    public SbHeaderFlags Flags;
    public uint CodeOfs;
    public uint IDsOfs;
    public uint IntPoolOfs;
    public uint FixedPoolOfs;
    public uint StringsOfs;
    public uint FuncsOfs;
    public uint PluginImportsOfs;
    public uint OCImportsOfs;
    public uint FuncImportsOfs;
    public uint StaticsOfs;
    public uint LocalPoolOfs;
    public uint SystemAtrOfs;
    public uint AttributesOfs;
    public uint DebugSymsOfs;

    public void Read(Span<byte> data)
    {
        SpanReader sr = new SpanReader(data);
        Magic = sr.ReadUInt32();
        Version = sr.ReadByte();
        A = sr.ReadByte();
        Flags = (SbHeaderFlags)sr.ReadByte();
        B = sr.ReadByte();
        CodeOfs = sr.ReadUInt32();
        IDsOfs = sr.ReadUInt32();
        IntPoolOfs = sr.ReadUInt32();
        FixedPoolOfs = sr.ReadUInt32();
        StringsOfs = sr.ReadUInt32();
        FuncsOfs = sr.ReadUInt32();
        PluginImportsOfs = sr.ReadUInt32();
        OCImportsOfs = sr.ReadUInt32();
        FuncImportsOfs = sr.ReadUInt32();
        StaticsOfs = sr.ReadUInt32();
        LocalPoolOfs = sr.ReadUInt32();
        SystemAtrOfs = sr.ReadUInt32();
        AttributesOfs = sr.ReadUInt32();
        DebugSymsOfs = sr.ReadUInt32();

        if (Flags.HasFlag(SbHeaderFlags.Scrambled))
            encodeScramble(data);
    }

    private void encodeScramble(Span<byte> data)
    {
        SpanReader sr = new SpanReader(data);

        // descramble ids
        sr.Position = (int)IDsOfs;
        uint idTableOffset = sr.ReadUInt32();
        uint numIdStrings = sr.ReadUInt32();
        uint idStringOffsetSize = sr.ReadUInt32();
        int actualIdStringsOffset = (int)(IDsOfs + idTableOffset + (numIdStrings * idStringOffsetSize));

        uint idStrSize = (uint)(IntPoolOfs - actualIdStringsOffset);
        encodeScrambleSub(data[actualIdStringsOffset..], idStrSize);

        // descramble strings
        sr.Position = (int)StringsOfs;
        uint strTableOffset = sr.ReadUInt32();
        uint numStrings = sr.ReadUInt32();
        uint stringOffsetSize = sr.ReadUInt32();
        int actualStringsOffset = (int)(StringsOfs + strTableOffset + (numStrings * stringOffsetSize));

        uint strsSize = (uint)(FuncsOfs - actualStringsOffset);
        encodeScrambleSub(data[actualStringsOffset..], strsSize);
    }

    private void encodeScrambleSub(Span<byte> data, uint size)
    {
        for (int i = 0; i < size; i += 4)
        {
            byte _1 = data[i];
            byte _2 = data[i + 1];
            byte _3 = data[i + 2];
            byte _4 = data[i + 3];

            if (size - i >= 1)
                data[i] = (byte)((_1 >> 2) + (_4 << 6));

            if (size - i >= 2)
                data[i + 1] = (byte)((_2 >> 2) + (_1 << 6));

            if (size - i >= 3)
                data[i + 2] = (byte)((_3 >> 2) + (_2 << 6));

            if (size - i >= 4)
                data[i + 3] = (byte)((_4 >> 2) + (_3 << 6));
        }

    }
}

public enum SbHeaderFlags : byte
{
    Loaded = 0x01,
    Scrambled = 0x02,
}
