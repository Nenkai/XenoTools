

using CommandLine;
using CommandLine.Text;
using XenoTools.BinaryData.ID;

using XenoTools.BinaryData;
using XenoTools.BinaryDataSQLite;

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

            var bdat = new Bdat();
            bdat.Regist(exportVerbs.InputPath);

            if (string.IsNullOrEmpty(exportVerbs.OutputPath))
                exportVerbs.OutputPath = $"{Path.GetFileNameWithoutExtension(exportVerbs.InputPath)}.sqlite";

            var _con = new SQLiteExporter();
            _con.InitConnection(exportVerbs.OutputPath);

            for (int i = 0; i < bdat.GetFileCount(); i++)
            {
                var file = bdat.GetFP(i);

                (List<BinaryDataIDMember> Layout, List<BinaryDataID> IDs) bdatFileLayoutAndID = file.GetLayoutAndIDs();
                _con.ExportTable(file.Name, bdatFileLayoutAndID.Layout, bdatFileLayoutAndID.IDs);
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
