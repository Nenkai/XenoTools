

using CommandLine;
using CommandLine.Text;
using XenoTools.BinaryData.ID;

using XenoTools.BinaryData;
using XenoTools.BinaryDataSQLite;
using XenoTools.Resources;
using XenoTools.Script;
using XenoTools.Script.Compiler;
using XenoTools.Script.Preprocessor;
using Esprima;
using NLog;
using System;

namespace XenoTools.CLI
{
    internal class Program
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public const string Version = "0.2.0";

        static void Main(string[] args)
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"- XenoTools {Version} by Nenkai");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("-----------------------------------------");

            var p = Parser.Default.ParseArguments<CompileScriptVerbs, DisassembleScriptVerbs, SQLiteExportVerbs/*, SQLiteImportVerbs*/>(args);

            p.WithParsed<CompileScriptVerbs>(CompileScript)
              .WithParsed<DisassembleScriptVerbs>(Disassemble)
              .WithParsed<SQLiteExportVerbs>(Export)
              /*.WithParsed<SQLiteImportVerbs>(Import)*/
              .WithNotParsed(HandleNotParsedArgs);

        }

        public static void Export(SQLiteExportVerbs exportVerbs)
        {
            if (string.IsNullOrEmpty(exportVerbs.InputPath) || !File.Exists(exportVerbs.InputPath))
            {
                Console.WriteLine("Provided input directory does not exist.");
                Environment.ExitCode = -1;
                return;
            }

            foreach (var ii in Directory.GetFiles(exportVerbs.InputPath, "*.bdat", SearchOption.TopDirectoryOnly))
            {
                var bdat = new Bdat();
                bdat.Regist(ii);

                exportVerbs.OutputPath = $"{Path.GetFileNameWithoutExtension(ii)}.sqlite";

                var _con = new SQLiteExporter();
                _con.InitConnection(ii + ".sqlite");

                for (int i = 0; i < bdat.GetFileCount(); i++)
                {
                    var file = bdat.GetFP(i);

                    (List<BinaryDataIDMember> Layout, List<BinaryDataID> IDs) bdatFileLayoutAndID = file.GetLayoutAndIDs();
                    _con.ExportTable(file.Name, bdatFileLayoutAndID.Layout, bdatFileLayoutAndID.IDs);
                }
            }
        }

        public static void CompileScript(CompileScriptVerbs compileScriptVerbs)
        {
            if (!File.Exists(compileScriptVerbs.InputPath))
            {
                Logger.Error($"File {compileScriptVerbs.InputPath} does not exist.");
                Environment.ExitCode = -1;
                return;
            }

            var source = File.ReadAllText(compileScriptVerbs.InputPath);
            var time = new FileInfo(compileScriptVerbs.InputPath).LastWriteTime;

            try
            {
                string absoluteIncludePath = Path.GetDirectoryName(compileScriptVerbs.InputPath);
                string sourceFile = compileScriptVerbs.InputPath;

                if (!string.IsNullOrWhiteSpace(compileScriptVerbs.BaseIncludeFolder))
                {
                    absoluteIncludePath = Path.GetFullPath(Path.Combine(absoluteIncludePath, compileScriptVerbs.BaseIncludeFolder)); // Returns baseIncludeFolder if it's absolute, otherwise applies it to the source file's location
                    sourceFile = Path.GetRelativePath(absoluteIncludePath, compileScriptVerbs.InputPath).Replace('\\', '/'); // Rewrite the path to be relative to the base folder, and normalise to forward slashes
                }

                var preprocessor = new ScriptPreprocessor();
                preprocessor.SetBaseDirectory(absoluteIncludePath);
                preprocessor.SetCurrentFileName(sourceFile);
                preprocessor.SetCurrentFileTimestamp(time);

                string preprocessed = preprocessor.Preprocess(source);
                if (compileScriptVerbs.PreprocessOnly)
                {
                    Console.Write(preprocessed);
                    Environment.ExitCode = 0;
                    return;
                }

                Logger.Info($"Started script build ({compileScriptVerbs.InputPath}).");

                var errorHandler = new ScriptErrorHandler();
                var parser = new AbstractSyntaxTree(preprocessed, new ParserOptions()
                {
                    ErrorHandler = errorHandler
                });
                parser.SetFileName(compileScriptVerbs.InputPath);

                var program = parser.ParseScript();
                if (errorHandler.HasErrors())
                {
                    foreach (ParseError error in errorHandler.Errors)
                        Logger.Error($"Syntax error: {error.Description} at {error.Source}:{error.LineNumber}");

                    Environment.ExitCode = -1;
                    return;
                }

                if (compileScriptVerbs.Release)
                    Logger.Info("Compiling as release - debug information will be removed from the script");
                
                if (compileScriptVerbs.DebugPrintFunctions)
                    Logger.Info("NOTE: Functions will have deb::put inserted");

                var compiler = new ScriptCompiler(compileScriptVerbs.InputPath, 
                    withDebugInformation: !compileScriptVerbs.Release,
                    debugPrintFunctions: compileScriptVerbs.DebugPrintFunctions);

                var state = compiler.Compile(program);
                PrintCompiledState(state);

                Logger.Info("Compiled, generating script file..");
                using (var outp = new FileStream(compileScriptVerbs.OutputPath, FileMode.Create))
                {
                    var gen = new ScriptCodeGen(state);
                    gen.Write(outp);

                    Logger.Info($"Generated. File size: {outp.Position} bytes.");
                }

                Logger.Info($"Script build successful.");
                Environment.ExitCode = 0;

                return;
            }
            catch (PreprocessorException preprocessException)
            {
                Logger.Error($"{preprocessException.FileName}:{preprocessException.Token.Location.Start.Line}: preprocess error: {preprocessException.Message}");
            }
            catch (ParserException parseException)
            {
                Logger.Error($"Syntax error: {parseException.Description} at {parseException.SourceText}:{parseException.LineNumber}");
            }
            catch (ScriptCompilationException compileException)
            {
                Logger.Error($"Compilation error: {compileException.Message}");
            }
            catch (Exception e)
            {
                Logger.Fatal(e, "Internal error in compilation");
            }

            Logger.Error("Script build failed.");
            Environment.ExitCode = -1;
        }

        public static void Disassemble(DisassembleScriptVerbs verbs)
        {
            if (!File.Exists(verbs.InputPath))
            {
                Logger.Error($"File {verbs.InputPath} does not exist.");
                Environment.ExitCode = -1;
                return;
            }

            var ogFile = File.ReadAllBytes(verbs.InputPath);
            var ogScript = new ScriptFile();
            ogScript.Read(ogFile);
            ogScript.Disassemble(verbs.OutputPath, verbs.CompareMode);

            Environment.ExitCode = 0;
        }

        public static void Import(SQLiteImportVerbs importVerbs)
        {

        }

        public static void HandleNotParsedArgs(IEnumerable<Error> errors)
        {

        }

        private static void PrintCompiledState(CompiledScriptState state)
        {
            Logger.Debug("State Info:");
            Logger.Debug("- Num Instructions: {numIdentifiers}", state.Code.Count);
            Logger.Debug("- Identifiers: {numIdentifiers}", state.IdentifierPool.Count);
            Logger.Debug("- Int Pool: {numIdentifiers}", state.IntPool.Count);
            Logger.Debug("- Fixed Pool: {numIdentifiers}", state.FixedPool.Count);
            Logger.Debug("- String Pool: {numIdentifiers}", state.StringPool.Count);
            Logger.Debug("- Func Pool: {numIdentifiers}", state.FuncPool.Count);
            Logger.Debug("- Plugin Import Pool: {numIdentifiers}", state.PluginImportPool.Count);
            Logger.Debug("- OCs Pool: {numIdentifiers}", state.OCPool.Count);
            Logger.Debug("- System Attributes Pool: {numIdentifiers}", state.SystemAttributes.Count);
        }
    }

    [Verb("script-compile", HelpText = "Compiles a script file (.sc/.si into .sb).")]
    public class CompileScriptVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input script source file.")]
        public string InputPath { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output script file.")]
        public string OutputPath { get; set; }

        [Option('e', Required = false, HelpText = "Preprocess only and output to stdout. Only for compiling scripts.")]
        public bool PreprocessOnly { get; set; }

        [Option('b', "base-include-folder", Required = false, HelpText = "Set the root path for #include statements.")]
        public string BaseIncludeFolder { get; set; }

        [Option("release", Required = false, HelpText = "Compiles as release - no debug information will be emitted to the script file.")]
        public bool Release { get; set; }

        [Option("debug-print-functions", Required = false, HelpText = "Inserts deb::put(<function>) name at the start of every function for debugging.")]
        public bool DebugPrintFunctions { get; set; }
    }

    [Verb("script-disassemble", HelpText = "Disassembles a script file (.sb).")]
    public class DisassembleScriptVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input script source file.")]
        public string InputPath { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output dissasembled file.")]
        public string OutputPath { get; set; }

        [Option("compare-mode", HelpText = "Whether to use compare mode - removes offsets/source files from disassembly, useful for matching instructions.")]
        public bool CompareMode { get; set; }
    }

    [Verb("bdat-to-sqlite", HelpText = "Export bdats to a SQLite files.")]
    public class SQLiteExportVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input folder with bdat files.")]
        public string InputPath { get; set; }

        [Option('o', "output", HelpText = "Output sqlite file.")]
        public string OutputPath { get; set; }
    }

    [Verb("sqlite-to-bdat", HelpText = "Imports sqlite into a bdat file (NOT YET IMPLEMENTED).")]
    public class SQLiteImportVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input sqlite file.")]
        public string InputPath { get; set; }

        [Option('o', "output", HelpText = "Output bdat file.")]
        public string OutputPath { get; set; }
    }
}
