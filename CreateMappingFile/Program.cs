using CommandLine;
using CommandLine.Text;
using System;
using System.Configuration;
using System.IO;
using System.Linq;

namespace CreateMappingFile
{
    internal class Options
    {
        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }

        [Option('o', "Output", Required = true, HelpText = "file location where results can be saved")]
        public string Output { get; set; }

        [Option('s', "connectionstring", HelpText = "ConnectionString to be used, if not specified the one in the .config file will be used. If the database does not exist it will be created.")]
        public string ConnectionString { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var usage = "Usage: CreateMappingFile.exe -i \"https://tfsserver:8080/tfs/DefaultCollection\" -o \"c:\\git\\mapping.txt\"";

            var help = new HelpText
            {
                Heading = "CreateMappingFile",

                AdditionalNewLineAfterOption = true,
                MaximumDisplayWidth = 100
            };
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine(usage);
            help.AddPreOptionsLine("");
            help.AddOptions(this);

            return help;
        }
    }

    internal class Program
    {
        private static void Main(string[] args)
        {
            var options = new Options();

            if (Parser.Default.ParseArguments(args, options))
            {
                string connectionstring;
                if (!string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    connectionstring = options.ConnectionString;
                }
                else
                {
                    connectionstring = ConfigurationManager.ConnectionStrings["VSOMigrDB"]?.ConnectionString;
                }

                using (var file = new StreamWriter(options.Output))
                {
                    file.WriteLine("Source ID|Target ID");

                    using (var ctx = new Data.VSOMigrDB(connectionstring))
                    {
                        foreach (var rev in ctx.WorkItemRevisions.Where(wil => wil.Migrated && wil.Revision == 0))
                        {
                            file.WriteLine(rev.OriginalId + "\t | \t" + rev.NewId);
                        }
                    }
                }

                if (!options.AutoClose)
                {
                    Console.WriteLine("push the <any> key to quit");
                    Console.ReadKey();
                }
            }
        }
    }
}