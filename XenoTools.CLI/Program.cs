

using CommandLine;
using CommandLine.Text;
using XenoTools.BinaryData.ID;

using XenoTools.BinaryData;
using XenoTools.BinaryDataSQLite;
using XenoTools.Resources;
using XenoTools.Script;
using XenoTools.Script.Compiler;

namespace XenoTools.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"- XenoTools by Nenkai");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("-----------------------------------------");

            var p = Parser.Default.ParseArguments<CompileScriptVerbs, SQLiteExportVerbs, SQLiteImportVerbs>(args);

            p.WithParsed<CompileScriptVerbs>(CompileScript)
              .WithParsed<SQLiteExportVerbs>(Export)
              .WithParsed<SQLiteImportVerbs>(Import)
              .WithNotParsed(HandleNotParsedArgs);

        }

        public static void Export(SQLiteExportVerbs exportVerbs)
        {
            if (string.IsNullOrEmpty(exportVerbs.InputPath) || !File.Exists(exportVerbs.InputPath))
            {
                Console.WriteLine("Provided input directory does not exist.");
                return;
            }

            foreach (var ii in Directory.GetFiles("D:\\Games\\Emu\\yuzu\\games\\0100FF500E34A000\\romfs\\bdat\\", "*.bdat", SearchOption.TopDirectoryOnly))
            {
                var bdat = new Bdat();
                bdat.Regist(ii);

                if (string.IsNullOrEmpty(ii))
                    exportVerbs.OutputPath = $"{Path.GetFileNameWithoutExtension(exportVerbs.InputPath)}.sqlite";

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

            var comp = new ScriptCompiler();
            var state = comp.Compile(File.ReadAllText(compileScriptVerbs.InputPath));

            using (var outp = new FileStream(compileScriptVerbs.OutputPath, FileMode.Create))
            { 
                var gen = new ScriptCodeGen(state);
                gen.Write(outp);
            }

            
            var f = File.ReadAllBytes(compileScriptVerbs.OutputPath);
            var h = new ScriptFile();
            h.Read(f);

        }
        public static void Import(SQLiteImportVerbs importVerbs)
        {

        }

        public static void HandleNotParsedArgs(IEnumerable<Error> errors)
        {

        }
    }

    [Verb("compile-script")]
    public class CompileScriptVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input script source file.")]
        public string InputPath { get; set; }

        [Option('o', "output", Required = true, HelpText = "Output script file.")]
        public string OutputPath { get; set; }
    }

    [Verb("bdat-to-sqlite", HelpText = "Export bdat to a SQLite file.")]
    public class SQLiteExportVerbs
    {
        [Option('i', "input", Required = true, HelpText = "Input bdat file.")]
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
