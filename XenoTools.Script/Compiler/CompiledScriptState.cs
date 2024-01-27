using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XenoTools.Script.Entities;

namespace XenoTools.Script.Compiler;

public class CompiledScriptState
{
    public uint EntryPointFunctionID { get; set; }
    public List<VMInstructionBase> Code { get; init; }
    public List<int> IntPool { get; init; }
    public List<float> FixedPool { get; init; }
    public List<string> StringPool { get; init; }
    public List<string> IdentifierPool { get; init; }
    public Dictionary<string, FunctionInfo> FuncPool { get; init; }
    public Dictionary<string, PluginImport> PluginImportPool { get; init; }
    public List<VmVariable> StaticPool { get; init; }
    public List<StackLocals> LocalPool { get; init; }
    public Dictionary<string, ObjectConstructor> OCPool { get; init; }
    public List<object> FuncImports { get; init; }
    public List<SystemAttribute> SystemAttributes { get; init; }
}
