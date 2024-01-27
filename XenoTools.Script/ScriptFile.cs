using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Memory;

namespace XenoTools.Script;

public class ScriptFile
{
    public SbHeader Header { get; set; } = new();

    public List<VMInstructionBase> Code { get; set; } = new();
    public List<string> Identifiers { get; set; } = new();
    public List<int> IntPool { get; set; } = new();
    public List<float> FixedPool { get; set; } = new();
    public List<string> StringPool { get; set; } = new();

    static ScriptFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public void Read(Span<byte> bytes)
    {
        Header.Read(bytes);

        SpanReader sr = new SpanReader(bytes);
        ReadCode(ref sr);
        ReadIdentifiers(ref sr);
        ReadIntPool(ref sr);
        ReadFixedPool(ref sr);
        ReadStringPool(ref sr);
    }

    private void ReadCode(ref SpanReader sr)
    {
        sr.Position = (int)Header.CodeOfs;
        int dataOffset = sr.ReadInt32();
        sr.ReadInt32(); // num entries - obviously 0
        int size = sr.ReadInt32();

        int actualCodeOffset = (int)(Header.CodeOfs + dataOffset);

        sr.Endian = Syroot.BinaryData.Core.Endian.Big;
        sr.Position = actualCodeOffset;
        while (sr.Position < actualCodeOffset + size)
        {
            VmInstType op = (VmInstType)sr.ReadByte();

            VMInstructionBase inst = VMInstructionBase.NewByType(op);
            inst.Offset = (sr.Position - actualCodeOffset) - 1;
            inst.Read(ref sr);

            Code.Add(inst);
        }

        sr.Endian = Syroot.BinaryData.Core.Endian.Little;
    }

    private void ReadIdentifiers(ref SpanReader sr)
    {
        sr.Position = (int)Header.IDsOfs;
        int entriesOffset = sr.ReadInt32();
        int numEntries = sr.ReadInt32();
        int offsetSize = sr.ReadInt32();

        sr.Position = (int)(Header.IDsOfs + entriesOffset);
        int[] offsets = new int[numEntries];
        for (int i = 0; i < numEntries; i++)
            offsets[i] = offsetSize == 2 ? sr.ReadInt16() : sr.ReadInt32();

        for (int i = 0; i < numEntries; i++)
        {
            sr.Position = (int)(Header.IDsOfs + entriesOffset + offsets[i]);
            string id = sr.ReadString0();
            Identifiers.Add(id);
        }
    }

    private void ReadIntPool(ref SpanReader sr)
    {
        sr.Position = (int)Header.IntPoolOfs;
        int entriesOffset = sr.ReadInt32();
        int numEntries = sr.ReadInt32();

        sr.Position = (int)(Header.IntPoolOfs + entriesOffset);

        for (int i = 0; i < numEntries; i++)
        {
            int @int = sr.ReadInt32();
            IntPool.Add(@int);
        }
    }

    private void ReadFixedPool(ref SpanReader sr)
    {
        sr.Position = (int)Header.FixedPoolOfs;
        int entriesOffset = sr.ReadInt32();
        int numEntries = sr.ReadInt32();

        sr.Position = (int)(Header.FixedPoolOfs + entriesOffset);

        for (int i = 0; i < numEntries; i++)
        {
            float @int = sr.ReadSingle();
            FixedPool.Add(@int);
        }
    }

    private void ReadStringPool(ref SpanReader sr)
    {
        sr.Encoding = Encoding.GetEncoding("Shift-JIS");
        sr.Position = (int)Header.StringsOfs;
        int entriesOffset = sr.ReadInt32();
        int numEntries = sr.ReadInt32();
        int offsetSize = sr.ReadInt32();

        sr.Position = (int)(Header.StringsOfs + entriesOffset);
        int[] offsets = new int[numEntries];
        for (int i = 0; i < numEntries; i++)
            offsets[i] = offsetSize == 2 ? sr.ReadInt16() : sr.ReadInt32();

        for (int i = 0; i < numEntries; i++)
        {
            sr.Position = (int)(Header.StringsOfs + entriesOffset + offsets[i]);
            string str = sr.ReadString0();
            StringPool.Add(str);
        }
    }
}
