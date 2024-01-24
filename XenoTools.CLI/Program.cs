

using CommandLine;
using CommandLine.Text;
using XenoTools.BinaryData.ID;

using XenoTools.BinaryData;
using XenoTools.BinaryDataSQLite;
using XenoTools.Resources;
using XenoTools.Script;

namespace XenoTools.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var f = File.ReadAllBytes(@"D:\Games\Emu\yuzu\games\0100FF500E34A000\romfs\script\vs20510100.sb");
            var h = new ScriptFile();
            h.Read(f);

            var comp = new ScriptCompiler();
            comp.Compile(@"
                function test()
                { 
                    var unitPC3 = unit(""player"", 3);
                    unitPC3.x = -38.93;
                    unitPC3.y = -1.91;
                    unitPC3.z = -7.5900002;
                } 
            
                function funcPsvLineAuto()
                {

                }
                ");

            /*
            var t = new ResLayImg();
            t.Read();
            */
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"- XenoTools by Nenkai");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("- https://github.com/Nenkai");
            Console.WriteLine("-----------------------------------------");

            var p = Parser.Default.ParseArguments<SQLiteExportVerbs, SQLiteImportVerbs>(args);

            p.WithParsed<SQLiteExportVerbs>(Export)
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

        public static void Import(SQLiteImportVerbs importVerbs)
        {

        }

        public static void HandleNotParsedArgs(IEnumerable<Error> errors)
        {

        }
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
