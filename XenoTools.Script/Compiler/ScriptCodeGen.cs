using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData;
using XenoTools.Script.Entities;

namespace XenoTools.Script.Compiler;

public class ScriptCodeGen
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private CompiledScriptState _compiledState;

    static ScriptCodeGen()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public ScriptCodeGen(CompiledScriptState state)
    {
        _compiledState = state;
    }

    public void Write(Stream stream)
    {
        var bs = new BinaryStream(stream);
        bs.WriteString("SB  ", StringCoding.Raw);
        bs.WriteByte(2);
        bs.WriteByte(0);
        bs.WriteByte(4);
        bs.WriteByte(0);

        bs.Position = 0x40; // Skip header for now.
        WriteCode(bs);
        WriteIdentifiers(bs);
        WriteIntPool(bs);
        WriteFixedPool(bs);
        WriteStrings(bs);
        WriteFunctions(bs);
        WritePluginImports(bs);
        WriteOCImports(bs);
        WriteFuncImports(bs);
        WriteStaticPool(bs);
        WriteLocalPool(bs);
        WriteSystemAttributes(bs);
    }

    private void WriteCode(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32(_compiledState.EntryPointFunctionID); // Different here - entrypoint func id
        bs.WriteUInt32(0); // size - we'll write it later

        long actualCodeOffset = bs.Position;

        // Code is big endian.
        bs.ByteConverter = ByteConverter.Big;
        {
            foreach (VMInstructionBase inst in _compiledState.Code)
            {
                bs.WriteByte((byte)inst.Type);
                inst.Write(bs);
            }
        }
        bs.ByteConverter = ByteConverter.Little;

        long lastInstOffset = bs.Position;
        bs.Position = tableOffset + 0x08;
        bs.WriteUInt32((uint)(lastInstOffset - actualCodeOffset));

        // Fill header
        bs.Position = 0x08;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastInstOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteIdentifiers(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.IdentifierPool.Count);
        bs.WriteUInt32(0); // size - we'll write it later

        bool asInts = false;
        int offsetTableSize = _compiledState.IdentifierPool.Count * sizeof(ushort);
        List<int> stringOffsets = new List<int>(_compiledState.IdentifierPool.Count);

        using var stringMemStream = new MemoryStream();
        using var stringBinStream = new BinaryStream(stringMemStream);
        for (int i = 0; i < _compiledState.IdentifierPool.Count; i++)
        {
            stringOffsets.Add((int)stringMemStream.Position);
            stringBinStream.WriteString(_compiledState.IdentifierPool[i], StringCoding.ZeroTerminated);
        }

        // calculate whether the offsets should be shorts or ints.
        // Start from the end
        for (int i = _compiledState.IdentifierPool.Count - 1; i >= 0; i--)
        {
            int strOffset = stringOffsets[i];
            if (offsetTableSize + strOffset > ushort.MaxValue)
            {
                // Offsets = ints
                asInts = true;
                offsetTableSize = _compiledState.IdentifierPool.Count * sizeof(uint);
                break;
            }
            else
            {
                // Offsets = ushorts
                break;
            }
        }

        // Actually write the offsets
        for(int i = 0; i < _compiledState.IdentifierPool.Count; i++)
        {
            if (asInts)
                bs.WriteInt32(offsetTableSize + stringOffsets[i]);
            else
                bs.WriteUInt16((ushort)(offsetTableSize + stringOffsets[i]));
        }

        // TODO scramble

        // Write the strings
        bs.Write(stringMemStream.ToArray());

        long lastOffset = bs.Position;

        bs.Position = tableOffset + 0x08;
        bs.WriteInt32(asInts ? sizeof(uint) : sizeof(ushort));

        // Fill header
        bs.Position = 0x0C;
        bs.WriteUInt32((uint)tableOffset);

        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteIntPool(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x08); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.IntPool.Count); // Num entries

        for (int i = 0; i < _compiledState.IntPool.Count; i++)
            bs.WriteInt32(_compiledState.IntPool[i]);

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x10;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteFixedPool(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x08); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.FixedPool.Count); // Num entries

        for (int i = 0; i < _compiledState.FixedPool.Count; i++)
            bs.WriteSingle(_compiledState.FixedPool[i]);

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x14;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteStrings(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.StringPool.Count);
        bs.WriteUInt32(0); // size - we'll write it later

        bool asInts = false;
        int offsetTableSize = _compiledState.StringPool.Count * sizeof(ushort);
        List<int> stringOffsets = new List<int>(_compiledState.StringPool.Count);

        using var stringMemStream = new MemoryStream();
        using var stringBinStream = new BinaryStream(stringMemStream);
        for (int i = 0; i < _compiledState.StringPool.Count; i++)
        {
            stringOffsets.Add((int)stringMemStream.Position);
            stringBinStream.WriteString(_compiledState.StringPool[i], StringCoding.ZeroTerminated, Encoding.GetEncoding("Shift-JIS"));
        }

        // calculate whether the offsets should be shorts or ints.
        // Start from the end
        for (int i = _compiledState.StringPool.Count - 1; i >= 0; i--)
        {
            int strOffset = stringOffsets[i];
            if (offsetTableSize + strOffset > ushort.MaxValue)
            {
                // Offsets = ints
                asInts = true;
                offsetTableSize = _compiledState.StringPool.Count * sizeof(uint);
                break;
            }
            else
            {
                // Offsets = ushorts
                break;
            }
        }

        // Actually write the offsets
        for (int i = 0; i < _compiledState.StringPool.Count; i++)
        {
            if (asInts)
                bs.WriteInt32(offsetTableSize + stringOffsets[i]);
            else
                bs.WriteUInt16((ushort)(offsetTableSize + stringOffsets[i]));
        }

        // TODO scramble

        // Write the strings
        bs.Write(stringMemStream.ToArray());

        long lastOffset = bs.Position;

        bs.Position = tableOffset + 0x08;
        bs.WriteInt32(asInts ? sizeof(uint) : sizeof(ushort));

        // Fill header
        bs.Position = 0x18;
        bs.WriteUInt32((uint)tableOffset);

        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteFunctions(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.FuncPool.Count); // Num entries
        bs.WriteUInt32((uint)FunctionInfo.GetSize()); // 

        int i = 0;
        foreach (var func in _compiledState.FuncPool.Values)
        {
            bs.WriteInt16(func.NameID);
            bs.WriteUInt16(func.NumArguments);
            bs.WriteBoolean(func.HasReturnValue, BooleanCoding.Word);
            bs.WriteUInt16(func.NumLocals);
            bs.WriteInt16(func.LocalPoolIndex);
            bs.WriteInt16(0);
            bs.WriteUInt32(func.CodeStartOffset);
            bs.WriteUInt32(func.CodeEndOffset);

            i++;
        }

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x1C;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WritePluginImports(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.PluginImportPool.Count); // Num entries
        bs.WriteUInt32(4); // size

        int i = 0;
        foreach (var import in _compiledState.PluginImportPool.Values)
        {
            bs.WriteUInt16(import.PluginNameIDIndex);
            bs.WriteUInt16(import.FunctionNameIDIndex);
            i++;
        }

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x20;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteOCImports(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)(_compiledState.OCPool?.Count ?? 0)); // Num entries
        bs.WriteUInt32(2); // size

        int i = 0;
        if (_compiledState.OCPool is not null)
        {
            foreach (var import in _compiledState.OCPool.Values)
            {
                bs.WriteUInt16((ushort)import.NameID);
                i++;
            }
        }

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x24;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteFuncImports(BinaryStream bs)
    {
        // TODO

        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)(_compiledState.FuncImports?.Count ?? 0)); // Num entries
        bs.WriteUInt32(4); // size

        int i = 0;
        if (_compiledState.FuncImports is not null)
        {
            foreach (var import in _compiledState.FuncImports)
            {
                bs.WriteInt32(0);
                i++;
            }
        }

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x28;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteStaticPool(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x8); // Relative Offset to actual code
        bs.WriteUInt32((uint)(_compiledState.StaticPool?.Count ?? 0)); // Num entries

        int i = 0;
        if (_compiledState.StaticPool is not null)
        {
            foreach (VmVariable var in _compiledState.StaticPool)
            {
                bs.WriteByte((byte)var.Type);
                bs.WriteByte(0);
                bs.WriteInt16((short)var.ArraySize);

                if (var.Type == LocalType.Int || var.Type == LocalType.String || var.Type == LocalType.Array)
                {
                    bs.WriteInt32((int)var.Value);
                }
                else if (var.Type == LocalType.Fixed)
                {
                    bs.WriteSingle((float)var.Value);
                }

                // This is directly copied to the stack so there's an extra 4 bytes (64 bit ptr on switch)
                bs.WriteUInt32(0);

                i++;
            }
        }

        long lastOffset = bs.Position;

        // Fill header
        bs.Position = 0x2C;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteLocalPool(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.LocalPool.Count); // Num entries
        bs.WriteUInt32(2); // size

        long offsetTableOffset = bs.Position;

        bs.Position += (_compiledState.LocalPool.Count * 2);
        bs.Align(0x04, grow: true);

        long lastOffset = bs.Position;
        for (int i = 0; i < _compiledState.LocalPool.Count; i++)
        {
            bs.Position = offsetTableOffset + (i * 0x02);
            bs.WriteUInt16((ushort)(lastOffset - offsetTableOffset));

            StackLocals stackLocals = _compiledState.LocalPool[i];
            bs.Position = lastOffset;
            bs.WriteUInt32(0x08);
            bs.WriteUInt32((uint)stackLocals.Locals.Count);

            foreach (VmVariable var in stackLocals.Locals.Values)
            {
                bs.WriteByte((byte)var.Type);
                bs.WriteByte(0);
                bs.WriteInt16((short)var.ArraySize);

                if (var.Type == LocalType.Int || var.Type == LocalType.String || var.Type == LocalType.Array)
                {
                    bs.WriteInt32((int)var.Value);
                }
                else if (var.Type == LocalType.Fixed)
                {
                    bs.WriteSingle((float)var.Value);
                }

                // This is directly copied to the stack so there's an extra 4 bytes (64 bit ptr on switch)
                bs.WriteUInt32(0);
            }

            lastOffset = bs.Position;
        }

        // Fill header
        bs.Position = 0x30;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }

    private void WriteSystemAttributes(BinaryStream bs)
    {
        long tableOffset = bs.Position;

        bs.WriteInt32(0x0C); // Relative Offset to actual code
        bs.WriteUInt32((uint)_compiledState.SystemAttributes.Count); // Num entries
        bs.WriteUInt32(2); // size

        long lastOffset = bs.Position;
        if (_compiledState.SystemAttributes is not null)
        {
            for (int i = 0; i < _compiledState.SystemAttributes.Count; i++)
                bs.WriteInt16(_compiledState.SystemAttributes[i].NameID);
        }

        // Fill header
        bs.Position = 0x34;
        bs.WriteUInt32((uint)tableOffset);

        // Move to end.
        bs.Position = lastOffset;
        bs.Align(0x04, grow: true);
    }
}
