using CommandLine;
using CommandLine.Text;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Utils;

namespace GenerateFieldMappings
{
    internal class Options
    {
        [Option('i', "SourceTfsCollectionUrl", Required = true, HelpText = "Url to the source Tfs Collection. https://tfsserver:8080/tfs/DefaultCollection")]
        public string SourceTfsCollectionUrl { get; set; }

        [Option('n', "TargetTfsCollectionUrl", Required = true, HelpText = "Url to the target (new) Tfs Collection.")]
        public string TargetTfsCollectionUrl { get; set; }

        [Option('p', "SourceProject", Required = true, HelpText = "Name of the source project")]
        public string SourceProjectName { get; set; }

        [Option('t', "TargetProject", Required = true, HelpText = "Name of the target (new) project")]
        public string TargetProjectName { get; set; }

        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }

        [Option('o', "Output", Required = true, HelpText = "file location where results can be saved")]
        public string Output { get; set; }

        [OptionList('m', "Mapping", Separator = ';', HelpText = "Manually add mappings between WorkItemTypes. E.g. Issue:Bug, will map Issue on the SourceProject to Bug on the TargetProject, usefull if you are doing cross process template migrations")]
        public IList<string> ManualMappings { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var usage = "Usage: GenerateFieldMappings.exe -i \"https://tfsserver:8080/tfs/DefaultCollection\" -p \"SourceProject\" -n \"https://tfsserver:8080/tfs/newDefaultCollection\" -t \"TargetProject\" -o \"D:\\migration\\migration_Sourceroject.xml\"";
            var usage2 = "Usage: GenerateFieldMappings.exe -i \"https://tfsserver:8080/tfs/DefaultCollection\" -p \"SourceProject\" -n \"https://tfsserver:8080/tfs/newDefaultCollection\" -t \"TargetProject\" -o \"D:\\migration\\migration_Sourceproject.xml\" -m \"User Story:Product Backlog Item;Issue:Bug\"";

            var help = new HelpText
            {
                Heading = "GenerateFieldMappings",
                AdditionalNewLineAfterOption = true,
                MaximumDisplayWidth = 100
            };
            help.AddPreOptionsLine("");
            help.AddPreOptionsLine(usage);
            help.AddPreOptionsLine(usage2);
            help.AddPreOptionsLine("");
            help.AddOptions(this);

            return help;
        }
    }

    public class Program
    {
        private static void Main(string[] args)
        {
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                var sourceTfsUrl = options.SourceTfsCollectionUrl;
                var targetTfsUrl = options.TargetTfsCollectionUrl;
                var sourceProjectName = options.SourceProjectName;
                var targetProjectName = options.TargetProjectName;

                var sourceProject = Utils.Utils.GetWorkItemStore(sourceTfsUrl, false).Projects[sourceProjectName];

                var targetProject = Utils.Utils.GetWorkItemStore(targetTfsUrl, false).Projects[targetProjectName];

                Dictionary<string, string> manualMappings = new Dictionary<string, string>();
                if (options.ManualMappings != null && options.ManualMappings.Count > 0)
                {
                    foreach (var manualMapping in options.ManualMappings)
                    {
                        var split = manualMapping.Split(':');
                        manualMappings.Add(split[0], split[1]);
                    }
                }
                List<WorkItemTypeMapping> workItemMappings = Utils.Utils.CreateWorkItemTypeMapping(sourceProject.WorkItemTypes, targetProject.WorkItemTypes, manualMappings);

                WorkItemStoreMapping wism = new WorkItemStoreMapping() { WorkItemTypeMapping = workItemMappings.ToArray() };

                XmlSerializer serializer = new XmlSerializer(typeof(WorkItemStoreMapping), new Type[] { typeof(WorkItemFieldMapping), typeof(WorkItemTypeMapping) });

                using (var sw = new StreamWriter(options.Output))
                {
                    serializer.Serialize(sw, wism);
                }

                List<WorkItemType> missingTargetWorkItemTypes = new List<WorkItemType>();
                List<WorkItemType> mappedSourceWorkItemTypes = new List<WorkItemType>();

                foreach (WorkItemType sourceWorkItemType in sourceProject.WorkItemTypes)
                {
                    Console.WriteLine("------");
                    var query = targetProject.WorkItemTypes.Cast<WorkItemType>().Where(p => p.Name == sourceWorkItemType.Name);
                    var targetWorkItemType = query.FirstOrDefault();
                    if (targetWorkItemType != null)
                    {
                        var missingOnTargetSite = sourceWorkItemType.FieldDefinitions.Cast<FieldDefinition>().Except(targetWorkItemType.FieldDefinitions.Cast<FieldDefinition>(), new FieldDefinitionComparer());

                        if (missingOnTargetSite.Any())
                        {
                            Console.WriteLine("Missing fields on SourceWorkItemType {0}", sourceWorkItemType.Name);
                            foreach (var missingField in missingOnTargetSite)
                            {
                                Console.WriteLine("\t" + missingField.ReferenceName);
                            }

                            var possibleReplacements = targetWorkItemType.FieldDefinitions.Cast<FieldDefinition>().Except(sourceWorkItemType.FieldDefinitions.Cast<FieldDefinition>(), new FieldDefinitionComparer());
                            if (possibleReplacements.Any())
                            {
                                Console.WriteLine("Unmapped fields on target site:");
                                foreach (var possibleReplacementField in possibleReplacements)
                                {
                                    Console.WriteLine("\t" + possibleReplacementField.ReferenceName);
                                }
                            }
                        }
                        mappedSourceWorkItemTypes.Add(targetWorkItemType);
                    }
                    else if (manualMappings.ContainsKey(sourceWorkItemType.Name))
                    {
                        Console.WriteLine($"Manual Mapping {sourceWorkItemType.Name}:{manualMappings[sourceWorkItemType.Name]} was added with the commandline");
                        mappedSourceWorkItemTypes.Add(targetWorkItemType);
                    }
                    else
                    {
                        Console.WriteLine($"targetWorkItemtype {sourceWorkItemType.Name} was missing on targetProject");
                        missingTargetWorkItemTypes.Add(sourceWorkItemType);
                    }
                }

                var unmappedTargetWorkItemTypes = new List<WorkItemType>();
                foreach (WorkItemType targetWorkItemType in targetProject.WorkItemTypes)
                {
                    if (!mappedSourceWorkItemTypes.Contains(targetWorkItemType) && !manualMappings.ContainsValue(targetWorkItemType.Name))
                    {
                        unmappedTargetWorkItemTypes.Add(targetWorkItemType);
                    }
                }

                Console.WriteLine("Unmapped WorkItemType on SourceProject: ");
                foreach (var wi in missingTargetWorkItemTypes)
                {
                    Console.WriteLine(wi.Name);
                }

                Console.WriteLine("Unmapped WorkItemType on TargetProject: ");
                foreach (var wi in unmappedTargetWorkItemTypes)
                {
                    Console.WriteLine(wi.Name);
                }

                Console.WriteLine("You can manually add mapping by using the --Mapping command line switch");

                if (!options.AutoClose)
                {
                    Console.WriteLine("push the <any> key to quit");
                    Console.ReadKey();
                }
            }
        }
    }
}