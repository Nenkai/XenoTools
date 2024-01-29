using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Memory;

using XenoTools.Script.Entities;
using XenoTools.Script.Instructions;

namespace XenoTools.Script;

public class ScriptFile
{
    public SbHeader Header { get; set; } = new();

    public List<VMInstructionBase> Code { get; set; } = new();
    public List<string> Identifiers { get; set; } = new();
    public List<int> IntPool { get; set; } = new();
    public List<float> FixedPool { get; set; } = new();
    public List<string> StringPool { get; set; } = new();
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<PluginImport> PluginImports { get; set; } = new();
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
        ReadFunctions(ref sr);
        ReadPluginImports(ref sr);
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

    private void ReadPluginImports(ref SpanReader sr)
    {
        sr.Position = (int)Header.PluginImportsOfs;
        int entriesOffset = sr.ReadInt32();
        int numEntries = sr.ReadInt32();
        int offsetSize = sr.ReadInt32();

        sr.Position = (int)(Header.PluginImportsOfs + entriesOffset);
        for (int i = 0; i < numEntries; i++)
        {
            var pluginImport = new PluginImport();
            pluginImport.PluginNameIDIndex = sr.ReadUInt16();
            pluginImport.FunctionNameIDIndex = sr.ReadUInt16();
            PluginImports.Add(pluginImport);
        }
    }

    private void ReadFunctions(ref SpanReader sr)
    {
        sr.Position = (int)Header.FuncsOfs;

        uint offset = sr.ReadUInt32();
        uint funcCount = sr.ReadUInt32();
        uint funcInfoSize = sr.ReadUInt32();

        for (int i = 0; i < funcCount; i++)
        {
            var func = new FunctionInfo();
            func.NameID = sr.ReadInt16();
            func.NumArguments = sr.ReadUInt16();
            sr.ReadUInt16();
            func.NumLocals = sr.ReadUInt16();
            func.LocalPoolIndex = sr.ReadInt16();
            sr.ReadInt16();
            func.CodeStartOffset = sr.ReadUInt32();
            func.CodeEndOffset = sr.ReadUInt32();

            Functions.Add(func);
        }
    }

    public void Disassemble(string output)
    {
        using var sw = new StreamWriter(output);

        for (int i = 0; i < Identifiers.Count; i++)
        {
            sw.WriteLine($"[{i}] - {Identifiers[i]}");
        }

        for (int i = 0; i < Functions.Count; i++)
        {
            FunctionInfo func = Functions[i];
            sw.WriteLine($"{Identifiers[func.NameID]}:");
            sw.WriteLine($"- Num Locals: {func.NumLocals}, Num Arguments: {func.NumArguments}, Returns Value: {func.HasReturnValue}:");
            DisassembleFunction(sw, i);
            sw.WriteLine(".endfunc");
            sw.WriteLine();
        }
    }

    public void DisassembleFunction(StreamWriter sw, int idx)
    {
        var func = Functions[idx];
        Span<VMInstructionBase> insts = GetFunctionCode(func);

        for (int i = 0; i < insts.Length; i++)
        {
            VMInstructionBase inst = insts[i];
            sw.Write($"    " + inst.Type);

            switch (inst.Type)
            {
                case VmInstType.CONST_I:
                    sw.Write($": {((VmConstInteger)inst).Value}");
                    break;
                case VmInstType.CONST_I_W:
                    sw.Write($": {((VmConstInteger_Word)inst).Value}");
                    break;
                case VmInstType.POOL_INT:
                    sw.Write($": {IntPool[((VmPoolInt)inst).IntIndex]}");
                    break;
                case VmInstType.POOL_INT_W:
                    sw.Write($": {IntPool[((VmPoolInt_Word)inst).IntIndex]}");
                    break;
                case VmInstType.POOL_FLOAT:
                    sw.Write($": {FixedPool[((VmPoolFloat)inst).FloatIndex]}");
                    break;
                case VmInstType.POOL_FLOAT_W:
                    sw.Write($": {FixedPool[((VmPoolFloat_Word)inst).FloatIndex]}");
                    break;
                case VmInstType.POOL_STR:
                    sw.Write($": {StringPool[((VmPoolString)inst).StringIndex]}");
                    break;
                case VmInstType.POOL_STR_W:
                    sw.Write($": {StringPool[((VmPoolString_Word)inst).StringIndex]}");
                    break;
                case VmInstType.LD:
                    sw.Write($": {((VmLoad)inst).LocalIndex}");
                    break;
                case VmInstType.ST:
                    sw.Write($": {((VmStore)inst).LocalIndex}");
                    break;
                case VmInstType.LD_ARG:
                    sw.Write($": {((VmLoadArgument)inst).ArgumentIndex}");
                    break;
                case VmInstType.ST_ARG:
                    sw.Write($": {((VmStoreArgument)inst).ArgumentIndex}");
                    break;
                case VmInstType.LD_STATIC:
                    break;
                case VmInstType.LD_STATIC_W:
                    break;
                case VmInstType.ST_STATIC:
                    break;
                case VmInstType.ST_STATIC_W:
                    break;
                case VmInstType.LD_AR:
                    break;
                case VmInstType.ST_AR:
                    break;
                case VmInstType.LD_FUNC:
                    break;
                case VmInstType.LD_FUNC_W:
                    break;
                case VmInstType.LD_PLUGIN:
                    break;
                case VmInstType.LD_PLUGIN_W:
                    break;
                case VmInstType.JMP:
                    sw.Write($": {((VmJump)inst).JumpRelativeOffset}");
                    break;
                case VmInstType.JPF:
                    sw.Write($": {((VmJumpFalse)inst).JumpRelativeOffset}");
                    break;
                case VmInstType.CALL:
                    break;
                case VmInstType.CALL_W:
                    break;
                case VmInstType.PLUGIN:
                    {
                        var plugInst = (VmPlugin)inst;
                        var pluginImport = PluginImports[plugInst.PluginImportIndex];
                        sw.Write($": {Identifiers[pluginImport.PluginNameIDIndex]}::{Identifiers[pluginImport.FunctionNameIDIndex]}");
                    }
                    break;
                case VmInstType.PLUGIN_W:
                    {
                        var plugInst = (VmPlugin_Word)inst;
                        var pluginImport = PluginImports[plugInst.PluginImportIndex];
                        sw.Write($": {Identifiers[pluginImport.PluginNameIDIndex]}::{Identifiers[pluginImport.FunctionNameIDIndex]}");
                    }
                    break;
                case VmInstType.CALL_FAR:
                    break;
                case VmInstType.CALL_FAR_W:
                    break;
                case VmInstType.GET_OC:
                    break;
                case VmInstType.GET_OC_W:
                    break;
                case VmInstType.GETTER:
                    break;
                case VmInstType.GETTER_W:
                    break;
                case VmInstType.SETTER:
                    break;
                case VmInstType.SETTER_W:
                    break;
                case VmInstType.SEND:
                    break;
                case VmInstType.SEND_W:
                    break;
                case VmInstType.SWITCH:
                    break;
                default:
                    break;
            }
            sw.WriteLine();
        }
    }

    private Span<VMInstructionBase> GetFunctionCode(FunctionInfo func)
    {
        int start = -1;
        int end = -1;
        for (int i = 0; i < Code.Count; i++)
        {
            if (Code[i].Offset >= func.CodeStartOffset)
            {
                start = i;
                break;
            }
        }

        for (int i = start; i < Code.Count; i++)
        {
            if (Code[i].Offset >= func.CodeEndOffset)
            {
                end = i;
                break;
            }
        }

        if (end == -1)
            end = Code.Count;

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Code).Slice(start, end - start);
    }
}
