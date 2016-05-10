using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CopyTestCases
{
    internal class Options
    {
        [Option('i', "SourceTfsCollectionUrl", Required = true)]
        public string SourceTfsCollectionUrl { get; set; }

        [Option('n', "TargetTfsCollectionUrl", Required = true, HelpText = "Url to the target (new) Tfs Collection.")]
        public string TargetTfsCollectionUrl { get; set; }

        [Option('p', "SourceProject", Required = true, HelpText = "Name of the source project")]
        public string SourceProjectName { get; set; }

        [Option('t', "TargetProject", Required = true, HelpText = "Name of the target (new) project")]
        public string TargetProjectName { get; set; }

        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }
    }


    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}
