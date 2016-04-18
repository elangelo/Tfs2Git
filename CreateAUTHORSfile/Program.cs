using CommandLine;
using CommandLine.Text;
using System;
using System.DirectoryServices;
using System.IO;

namespace CreateAUTHORSfile
{
    internal class Options
    {
        [Option('i', "SourceTfsCollectionUrl", Required = true, HelpText = "Url to the source Tfs Collection. https://tfsserver:8080/tfs/tfscollection")]
        public string SourceTfsCollectionUrl { get; set; }

        [Option('p', "SourceProject", Required = true, HelpText = "Name of the source project")]
        public string SourceProjectName { get; set; }

        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }

        [Option('o', "AuthorsFile", Required = true, HelpText = "Filename where we will save the detected AUTHORS")]
        public string AuthorsFile { get; set; }

        [Option('d', "DefaultEmailAddress", HelpText = "If author is not found in AD then this email address will be used")]
        public string DefaultEmailAddress { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var usage = "Usage: CreateAUTHORSfile.exe -i \"http://tfsserver:8080/tfs/tfscollection\" -p \"SuperProject\" -o \"c:\\migration\\AUTHORS\" -d \"tfs@home.org\"";

            var help = new HelpText
            {
                Heading = "CreateAUTHORSfile",
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
                var sourceTfsUrl = options.SourceTfsCollectionUrl;
                var sourceProjectName = options.SourceProjectName;

                var authors = Utils.Utils.GetAllAuthors(sourceTfsUrl, sourceProjectName);
                using (var sw = new StreamWriter(options.AuthorsFile))
                {
                    foreach (var author in authors)
                    {
                        var username = author.Value.Split('\\')[1];

                        var email = GetEmailAddress(username);
                        if (string.IsNullOrEmpty(email))
                        {
                            if (string.IsNullOrEmpty(options.DefaultEmailAddress))
                            {
                                Console.WriteLine($"Could not find user {username} and the DefaultEmailAddresss argument is empty, can not continue....");

                                if (!options.AutoClose)
                                {
                                    Console.WriteLine("push the <any> key to quit");
                                    Console.ReadKey();
                                }

                                Environment.Exit(1);
                            }
                            email = options.DefaultEmailAddress;
                        }
                        sw.WriteLine($"{author.Value} = {author.Key} < {email} >");
                    }
                }

                if (!options.AutoClose)
                {
                    Console.WriteLine("push the <any> key to quit");
                    Console.ReadKey();
                }
            }
        }

        private static string GetEmailAddress(string username)
        {
            var search = new DirectorySearcher();
            search.Filter = $"samaccountname={username}";
            var result = search.FindOne();

            if (result != null && result.Properties.Contains("mail"))
            {
                return result.Properties["mail"][0].ToString();
            }
            else
            {
                Console.WriteLine($"not found: {username}");
                return "";
            }
        }
    }
}