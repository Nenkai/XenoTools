using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Syroot.BinaryData.Memory;

using XenoTools.Script.Entities;
using XenoTools.Script.Entities.Debugging;
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
    public List<ObjectConstructor> OCImports { get; set; } = new();

    public DebugInfo DebugInfo { get; set; } = new();

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
        ReadOCImports(ref sr);
        ReadDebugSymbols(ref sr);
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
            func.HasReturnValue = sr.ReadUInt16() == 1;
            func.NumLocals = sr.ReadUInt16();
            func.LocalPoolIndex = sr.ReadInt16();
            sr.ReadInt16();
            func.CodeStartOffset = sr.ReadUInt32();
            func.CodeEndOffset = sr.ReadUInt32();

            Functions.Add(func);
        }
    }

    private void ReadOCImports(ref SpanReader sr)
    {
        sr.Position = (int)Header.OCImportsOfs;

        uint offset = sr.ReadUInt32();
        uint numEntries = sr.ReadUInt32();
        uint entriesSize = sr.ReadUInt32();

        sr.Position = (int)(Header.OCImportsOfs + offset);

        for (int i = 0; i < numEntries; i++)
        {
            var oc = new ObjectConstructor();
            oc.ID = i;
            oc.NameID = sr.ReadInt16();

            OCImports.Add(oc);
        }
    }

    private void ReadDebugSymbols(ref SpanReader sr)
    {
        if (Header.DebugSymsOfs == 0)
            return;

        sr.Position = (int)Header.DebugSymsOfs;
        DebugInfo.Read(ref sr);
    }

    public void Disassemble(string output, bool asCompareMode = false)
    {
        using var sw = new StreamWriter(output);

        /*
        for (int i = 0; i < Identifiers.Count; i++)
        {
            sw.WriteLine($"[{i}] - {Identifiers[i]}");
        }
        */

        for (int i = 0; i < Functions.Count; i++)
        {
            FunctionInfo func = Functions[i];
            sw.WriteLine($"{Identifiers[func.NameID]}:");
            sw.WriteLine($"- Num Locals: {func.NumLocals}, Num Arguments: {func.NumArguments}, Returns Value: {func.HasReturnValue}:");
            DisassembleFunction(sw, i, asCompareMode);
            sw.WriteLine(".endfunc");
            sw.WriteLine();
        }
    }

    public void DisassembleFunction(StreamWriter sw, int idx, bool asCompareMode = false)
    {
        var func = Functions[idx];
        Span<VMInstructionBase> insts = GetFunctionCode(func);

        for (int i = 0; i < insts.Length; i++)
        {
            VMInstructionBase inst = insts[i];
            if (!asCompareMode)
            {
                if (DebugInfo.Lines.TryGetValue(inst.Offset, out DebugInfo.LineInfo lineInfo))
                {
                    string fileName = DebugInfo.FileNames[lineInfo.SourceNameID];
                    sw.WriteLine($"; {fileName}:{lineInfo.LineNumber}");
                }
            }

            if (asCompareMode)
                sw.Write($"    " + inst.Type);
            else
                sw.Write($"    {inst.Offset:X4}|" + inst.Type);

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
                    sw.Write($": \"{StringPool[((VmPoolString)inst).StringIndex]}\"");
                    break;
                case VmInstType.POOL_STR_W:
                    sw.Write($": \"{StringPool[((VmPoolString_Word)inst).StringIndex]}\"");
                    break;
                case VmInstType.LD:
                    {
                        var load = (VmLoad)inst;
                        PrintLocal(sw, inst, idx, load.LocalIndex);
                    }
                    break;
                case VmInstType.ST:
                    {
                        var store = (VmStore)inst;
                        PrintLocal(sw, inst, idx, store.LocalIndex);
                    }
                    break;
                case VmInstType.LD_ARG:
                    sw.Write($": {((VmLoadArgument)inst).ArgumentIndex}");
                    break;
                case VmInstType.ST_ARG:
                    sw.Write($": {((VmStoreArgument)inst).ArgumentIndex}");
                    break;
                case VmInstType.LD_STATIC:
                    {
                        var loadStatic = ((VmLoadStatic)inst);
                        PrintStatic(sw, loadStatic, loadStatic.StaticIndex);
                    }
                    break;
                case VmInstType.LD_STATIC_W:
                    {
                        var loadStatic = ((VmLoadStatic_Word)inst);
                        PrintStatic(sw, loadStatic, loadStatic.StaticIndex);
                    }
                    break;
                case VmInstType.ST_STATIC:
                    {
                        var storeStatic = ((VmStoreStatic)inst);
                        PrintStatic(sw, storeStatic, storeStatic.StaticIndex);
                    }
                    break;
                case VmInstType.ST_STATIC_W:
                    {
                        var storeStatic = ((VmStoreStatic_Word)inst);
                        PrintStatic(sw, storeStatic, storeStatic.StaticIndex);
                    }
                    break;
                case VmInstType.LD_AR:
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
                    if (asCompareMode)
                        sw.Write($": {((VmJump)inst).JumpRelativeOffset}");
                    else
                        sw.Write($": {inst.Offset + ((VmJump)inst).JumpRelativeOffset:X4}");
                    break;
                case VmInstType.JPF:
                    if (asCompareMode)
                        sw.Write($": {((VmJumpFalse)inst).JumpRelativeOffset}");
                    else
                        sw.Write($": {inst.Offset + ((VmJumpFalse)inst).JumpRelativeOffset:X4}");
                    break;
                case VmInstType.CALL:
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
                case VmInstType.CALL_FAR_W:
                    break;
                case VmInstType.GET_OC:
                    {
                        var getOc = (VmGetOC)inst;
                        var oc = OCImports[getOc.OCIndex];
                        sw.Write($": {Identifiers[oc.NameID]}");
                    }
                    break;
                case VmInstType.GET_OC_W:
                    {
                        var getOc = (VmGetOC_Word)inst;
                        var oc = OCImports[getOc.OCIndex];
                        sw.Write($": {Identifiers[oc.NameID]}");
                    }
                    break;
                case VmInstType.GETTER:
                    {
                        var getter = (VmGetter)inst;
                        sw.Write($": {Identifiers[getter.IDIndex]}");
                    }
                    break;
                case VmInstType.GETTER_W:
                    {
                        var getter = (VmGetter_Word)inst;
                        sw.Write($": {Identifiers[getter.IDIndex]}");
                    }
                    break;
                case VmInstType.SETTER:
                    {
                        var setter = (VmSetter)inst;
                        sw.Write($": {Identifiers[setter.IDIndex]}");
                    }
                    break;
                case VmInstType.SETTER_W:
                    {
                        var setter = (VmSetter_Word)inst;
                        sw.Write($": {Identifiers[setter.IDIndex]}");
                    }
                    break;
                case VmInstType.SEND:
                    sw.Write($": {Identifiers[((VmSend)inst).IDIndex]}");
                    break;
                case VmInstType.SEND_W:
                    sw.Write($": {Identifiers[((VmSend_Word)inst).IDIndex]}");
                    break;
                case VmInstType.SWITCH:
                    sw.Write($": {string.Join(", ", ((VmSwitch)inst).Branches.Select(e => e.Case))}");
                    break;
                default:
                    break;
            }
            sw.WriteLine();
        }
    }

    private void PrintStatic(StreamWriter sw, VMInstructionBase inst, int staticIndex)
    {
        if (DebugInfo?.StaticSyms?.ContainsKey(staticIndex) == true)
        {
            DebugInfoVariable debugSym = DebugInfo.StaticSyms[staticIndex];
            string name = Identifiers[debugSym.NameID];
            sw.Write($": {name}");
        }
        else
            sw.Write($": (Index: {staticIndex}");
    }

    private void PrintLocal(StreamWriter sw, VMInstructionBase inst, int funcIdx, int localIdx)
    {
        var local = DebugInfo?.FunctionLocalSymbols?[funcIdx].Locals?[localIdx];

        if (local is not null)
        {
            string name = Identifiers[local.NameID];
            sw.Write($": {name}");
        }
        else
            sw.Write($": (Index: {local}");
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
                end = i + 1;
                break;
            }
        }

        if (end == -1)
            end = Code.Count;

        return System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Code).Slice(start, end - start);
    }
}
