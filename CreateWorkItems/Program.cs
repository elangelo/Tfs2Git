using CommandLine;
using CommandLine.Text;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Utils;

namespace CreateWorkItems
{
    internal class Options
    {
        [Option('i', "SourceTfsCollectionUrl", Required = true, HelpText = "Url to the source Tfs Collection. https://tfsserver:8080/tfs/tfscollection")]
        public string SourceTfsCollectionUrl { get; set; }

        [Option('n', "TargetTfsCollectionUrl", Required = true, HelpText = "Url to the target (new) Tfs Collection.")]
        public string TargetTfsCollectionUrl { get; set; }

        [Option('p', "SourceProject", Required = true, HelpText = "Name of the source project")]
        public string SourceProjectName { get; set; }

        [Option('t', "TargetProject", Required = true, HelpText = "Name of the target (new) project")]
        public string TargetProjectName { get; set; }

        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }

        [Option('w', "WorkItemMappingFile", Required = true, HelpText = "File location where results from GenerateFieldMappings is stored")]
        public string WorkItemMappingFile { get; set; }

        [Option('s', "connectionstring", HelpText = "ConnectionString to be used, if not specified the one in the .config file will be used. If the database does not exist it will be created.")]
        public string ConnectionString { get; set; }

        [Option('l', "LogFile", Required = true, HelpText = "file where log of the process will be written")]
        public string LogFile { get; set; }

        [Option('a', "CloneAreasAndIterations", DefaultValue = false)]
        public bool CloneAreasAndIterations { get; set; }

        [Option('f', "CloneWorkItems", DefaultValue = false)]
        public bool CloneWorkItems { get; set; }

        [Option('q', "CloneQueries", DefaultValue = false)]
        public bool CloneQueries { get; set; }

        [Option('e', "CloneTestPlans", DefaultValue = false)]
        public bool CloneTestPlans { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var usage = "Usage: CreateWorkItems.exe -i \"https://tfsserver:8080/tfs/tfscollection\" -p \"SuperProject\" -n \"https://tfsserver:8080/tfs/newtfscollection\" -t \"SuperProject_NG\" -o \"D:\\migration\\migration_Superproject.xml\"";

            var help = new HelpText
            {
                Heading = "CreateWorkItems",
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
                var initialPosition = Console.CursorTop;
                string workItemMappingFile = options.WorkItemMappingFile;

                bool cloneAreasAndIterations = options.CloneAreasAndIterations;
                bool cloneWorkItems = options.CloneWorkItems;
                bool cloneQueries = options.CloneQueries;
                bool cloneTestPlans = options.CloneTestPlans;

                var sourceTfsUrl = options.SourceTfsCollectionUrl;
                var targetTfsUrl = options.TargetTfsCollectionUrl;
                var sourceProjectName = options.SourceProjectName;
                var targetProjectName = options.TargetProjectName;

                string connectionstring;
                if (!string.IsNullOrWhiteSpace(options.ConnectionString))
                {
                    connectionstring = options.ConnectionString;
                }
                else
                {
                    connectionstring = ConfigurationManager.ConnectionStrings["VSOMigrDB"]?.ConnectionString;
                }

                if (string.IsNullOrWhiteSpace(connectionstring))
                {
                    throw new Exception("connectionstring was not valid");
                }

                Dictionary<int, string> invalidWorkItems = new Dictionary<int, string>();
                var targetWorkitemStore = Utils.Utils.GetWorkItemStore(targetTfsUrl, true);
                var stopwatch = Stopwatch.StartNew();
                if (cloneAreasAndIterations)
                {
                    if (Utils.Utils.GetWorkItems(targetWorkitemStore, targetProjectName).Count > 0)
                    {
                        Console.WriteLine("there are already workitems in the project, cloning areas an iterations will destroy previous work!");
                        Environment.Exit(1);
                    }

                    Utils.Utils.CopyAreaAndIterationNodes(sourceTfsUrl, sourceProjectName, targetTfsUrl, targetProjectName);
                }

                if (cloneWorkItems)
                {
                    WorkItemStoreMapping wism = Utils.Utils.ReadWorkItemMappingFile(workItemMappingFile);

                    targetWorkitemStore = Utils.Utils.GetWorkItemStore(targetTfsUrl, true);
                    var sourceWorkitemStore = Utils.Utils.GetWorkItemStore(sourceTfsUrl, false);

                    var targetProject = targetWorkitemStore.Projects[targetProjectName];
                    var sourceProject = sourceWorkitemStore.Projects[sourceProjectName];
                    var areaNodeMap = Utils.Utils.GetNodeMap(sourceProject.AreaRootNodes, targetProject.AreaRootNodes);
                    var iterationNodeMap = Utils.Utils.GetNodeMap(sourceProject.IterationRootNodes, targetProject.IterationRootNodes);

                    var mapping = new Dictionary<int, int>();
                    var notLinkedYet = 0;
                    var exceptions = new List<string>();
                    var retryIdx = 0;
                    bool done = false;
                    while (retryIdx < 10 && !done)
                    {
                        try
                        {
                            using (var ctx = new Data.VSOMigrDB(connectionstring))
                            {
                                //regenerate mapping
                                mapping = ctx.WorkItemRevisions.Where(t => t.Migrated && t.Revision == 0 && t.NewId != 0).ToDictionary(wir => wir.OriginalId, wir => wir.NewId);
                                var revisionOperations = ctx.WorkItemRevisions.Where(t => !t.Migrated && t.Project == sourceProjectName).OrderBy(t => t.Changed).ThenBy(t => t.Revision).ToList();
                                var count = revisionOperations.Count();
                                int counter = 0;
                                foreach (var wiRev in revisionOperations)
                                {
                                    if (!invalidWorkItems.ContainsKey(wiRev.OriginalId))
                                    {
                                        counter++;
                                        Console.SetCursorPosition(0, initialPosition);
                                        Console.WriteLine(counter + "/" + count);

                                        if (wiRev.Kind == "link")
                                        {
                                            var targetWorkItem = targetWorkitemStore.GetWorkItem(mapping[wiRev.OriginalId]);
                                            var sourceWorkItem = sourceWorkitemStore.GetWorkItem(wiRev.OriginalId);

                                            var sourceWorkItemLinkHistoryRev = sourceWorkItem.WorkItemLinkHistory.Cast<WorkItemLink>().Where(wil => wil.RemovedDate.Year == 9999 && wil.TargetId.ToString() == wiRev.ChangedFields).First();

                                            if (mapping.ContainsKey(sourceWorkItemLinkHistoryRev.TargetId))
                                            {
                                                if (targetWorkitemStore.GetWorkItem(mapping[sourceWorkItemLinkHistoryRev.TargetId]).WorkItemLinks.Cast<WorkItemLink>().Where(wil => wil.TargetId == targetWorkItem.Id || wil.SourceId == targetWorkItem.Id).Any())
                                                {
                                                    //Link exists already.
                                                }
                                                else
                                                {
                                                    var linkTypeEnd = targetWorkitemStore.WorkItemLinkTypes.LinkTypeEnds[sourceWorkItemLinkHistoryRev.LinkTypeEnd.Name];
                                                    var workItemLink = new WorkItemLink(linkTypeEnd, mapping[sourceWorkItemLinkHistoryRev.SourceId], mapping[sourceWorkItemLinkHistoryRev.TargetId]);
                                                    workItemLink.ChangedDate = sourceWorkItemLinkHistoryRev.ChangedDate;
                                                    targetWorkItem.WorkItemLinks.Add(workItemLink);

                                                    var errors = targetWorkItem.Validate();
                                                    if (errors.Count == 0)
                                                    {
                                                        targetWorkItem.Save();
                                                    }
                                                    else
                                                    {
                                                        invalidWorkItems.Add(wiRev.OriginalId, string.Join(Environment.NewLine, errors.Cast<Field>().Select(f => f.ReferenceName + " " + f.Status.ToString())));
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                notLinkedYet++;
                                                continue;
                                            }
                                        }
                                        else if (wiRev.Kind == "revision")
                                        {
                                            var sourceWorkItem = sourceWorkitemStore.GetWorkItem(wiRev.OriginalId);
                                            var sourceWiRev = sourceWorkItem.Revisions[wiRev.Revision];

                                            bool isNew = false;
                                            WorkItem targetWorkItem;
                                            if (wiRev.Revision == 0)
                                            {
                                                var workItemTypeMapping = wism.WorkItemTypeMapping.Where(p => p.SourceWorkItemType == sourceWorkItem.Type.Name).FirstOrDefault();
                                                if (workItemTypeMapping != null)
                                                {
                                                    var targetWorkItemTypeName = workItemTypeMapping.TargetWorkItemType;
                                                    targetWorkItem = new WorkItem(targetProject.WorkItemTypes[targetWorkItemTypeName]);
                                                    isNew = true;
                                                }
                                                else
                                                {
                                                    Console.WriteLine($"{sourceWorkItem.Type.Name} was not found in the WorkItemMappingFile");
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                targetWorkItem = targetWorkitemStore.GetWorkItem(mapping[wiRev.OriginalId]);
                                            }

                                            Utils.Utils.CopyFields(sourceWiRev, targetWorkItem, areaNodeMap, iterationNodeMap, targetProject, sourceProject, wism, isNew);

                                            if (targetWorkItem.Fields[CoreField.ChangedDate].Value != null)
                                            {
                                                if (targetWorkItem.Fields[CoreField.ChangedDate].OriginalValue != null)
                                                {
                                                    if ((DateTime)targetWorkItem.Fields[CoreField.ChangedDate].Value < (DateTime)targetWorkItem.Fields[CoreField.ChangedDate].OriginalValue)
                                                    {
                                                        Trace.WriteLine("PROBLEM!!!");
                                                        //problem!!!
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                Trace.WriteLine("WTF!!");
                                            }

                                            var errors = targetWorkItem.Validate();
                                            if (errors.Count == 0)
                                            {
                                                targetWorkItem.Save();
                                                if (isNew)
                                                {
                                                    mapping.Add(sourceWorkItem.Id, targetWorkItem.Id);
                                                    wiRev.NewId = targetWorkItem.Id;
                                                }
                                                wiRev.Migrated = true;
                                            }
                                            else
                                            {
                                                invalidWorkItems.Add(wiRev.OriginalId, string.Join(Environment.NewLine, errors.Cast<Field>().Select(f => f.ReferenceName + " " + f.Status.ToString())));
                                            }
                                        }
                                        ctx.SaveChanges();
                                    }
                                }
                                done = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            retryIdx++;
                            Console.SetCursorPosition(0, initialPosition + 2);
                            Console.WriteLine("retry index: {0}", retryIdx);
                            Console.SetCursorPosition(0, initialPosition + 3);
                            Console.WriteLine(ex.ToString());
                            exceptions.Add(ex.ToString());
                            Thread.Sleep(TimeSpan.FromSeconds(60 * retryIdx));
                            done = false;
                        }
                    }

                    using (var sw = new StreamWriter(options.LogFile))
                    {
                        sw.WriteLine("{0} links not yet done!", notLinkedYet);
                        sw.WriteLine("Elapsed seconds: " + stopwatch.Elapsed.TotalSeconds);
                        sw.WriteLine("retries: " + retryIdx);
                        foreach (var ex in exceptions)
                        {
                            sw.WriteLine(ex);
                        }

                        foreach (var kvp in invalidWorkItems)
                        {
                            sw.WriteLine(kvp.Key + ": " + kvp.Value);
                        }
                    }
                }

                if (cloneTestPlans)
                {
                    Dictionary<int, int> mapping;
                    using (var ctx = new Data.VSOMigrDB(connectionstring))
                    {
                        mapping = ctx.WorkItemRevisions.Where(t => t.Migrated && t.Revision == 0 && t.NewId != 0).ToDictionary(wir => wir.OriginalId, wir => wir.NewId);
                    }

                    var testPlanMigration = new TestPlanMigration(sourceTfsUrl, targetTfsUrl, sourceProjectName, targetProjectName, mapping);
                    testPlanMigration.CopyTestPlans();
                }

                if (cloneQueries)
                {
                    var targetTuple = Utils.Utils.GetShareQueryFolder(targetTfsUrl, targetProjectName);

                    var sourceTuple = Utils.Utils.GetShareQueryFolder(sourceTfsUrl, sourceProjectName);

                    Utils.Utils.CopySubStuff(sourceTuple.Item2, targetTuple.Item2);

                    targetTuple.Item1.QueryHierarchy.Save();
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