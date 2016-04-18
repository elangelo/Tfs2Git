using CommandLine;
using CommandLine.Text;
using Data;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;

namespace BuildHistoryInDatabase
{
    internal class Options
    {
        [Option('i', "tfsCollectionUrl", Required = true, HelpText = "Url to the Tfs Collection. https://tfsserver:8080/tfs/tfscollection")]
        public string TfsCollectionUrl { get; set; }

        [Option('p', "project", Required = true, HelpText = "Project to be processed")]
        public string TfsProject { get; set; }

        [Option('s', "connectionstring", HelpText = "ConnectionString to be used, if not specified the one in the .config file will be used. If the database does not exist it will be created.")]
        public string ConnectionString { get; set; }

        [Option('c', "close", DefaultValue = false, HelpText = "If put to true the command will not wait for userinput after executing")]
        public bool AutoClose { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var usage = "Usage: BuildHistoryInDatabase.exe -i \"https://tfsserver:8080/tfs/tfscollection\" -p \"TFS\"";

            var help = new HelpText
            {
                Heading = "BuildHistoryInDatabase",
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
            var initialPosition = Console.CursorTop;
            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                var stopwatch = Stopwatch.StartNew();

                var tfsTeamFoundationServerUrl = options.TfsCollectionUrl;
                var projectName = options.TfsProject;

                var workItemStore = Utils.Utils.GetWorkItemStore(tfsTeamFoundationServerUrl, false);
                var workItems = Utils.Utils.GetWorkItems(workItemStore, projectName);
                var totalNumberOfWorkItems = workItems.Count;
                var revCount = 0;
                var wiCount = 0;

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

                using (Data.VSOMigrDB ctx = new Data.VSOMigrDB(connectionstring))
                {
                    List<WorkItemRevision> tmpStorage = new List<WorkItemRevision>();

                    foreach (WorkItem wi in workItems)
                    {
                        wiCount++;
                        Console.SetCursorPosition(0, initialPosition);
                        Console.Write(wiCount + "/" + totalNumberOfWorkItems);

                        for (int i = 0; i < wi.Revisions.Count; i++)
                        {
                            var rev = wi.Revisions[i];

                            string changedFields = GetChangedFields(rev);

                            var revision = new WorkItemRevision()
                            {
                                Changed = (DateTime)rev.Fields[CoreField.ChangedDate].Value,
                                OriginalId = wi.Id,
                                Migrated = false,
                                Revision = i,
                                RevisionCount = wi.Revisions.Count + wi.WorkItemLinkHistory.Count,
                                Kind = "revision",
                                Project = projectName,
                                ChangedFields = changedFields
                            };

                            tmpStorage.Add(revision);

                            revCount++;
                            if (revCount % 100 == 0)
                            {
                                ctx.WorkItemRevisions.AddRange(tmpStorage);
                                ctx.SaveChanges();
                                Console.SetCursorPosition(0, initialPosition + 1);
                                Console.Write("Revision count: " + revCount);
                                tmpStorage = new List<WorkItemRevision>();
                            }
                        }

                        for (int i = 0; i < wi.WorkItemLinkHistory.Count; i++)
                        {
                            var rev = wi.WorkItemLinkHistory[i];

                            //workitemlink was not removed.
                            if (wi.WorkItemLinkHistory[i].RemovedBy == "TFS Everyone" && wi.WorkItemLinkHistory[i].RemovedDate.Year == 9999)
                            {
                                var revision = new WorkItemRevision()
                                {
                                    Changed = rev.AddedDate,
                                    OriginalId = wi.Id,
                                    Migrated = false,
                                    Revision = i + wi.Revisions.Count,
                                    RevisionCount = wi.Revisions.Count + wi.WorkItemLinkHistory.Count,
                                    ChangedFields = rev.TargetId.ToString(),
                                    Kind = "link",
                                    Project = projectName
                                };
                                revCount++;
                                tmpStorage.Add(revision);
                                if (revCount % 100 == 0)
                                {
                                    ctx.WorkItemRevisions.AddRange(tmpStorage);
                                    ctx.SaveChanges();
                                    Console.SetCursorPosition(0, initialPosition + 1);
                                    Console.Write("Revision count: " + revCount);
                                    tmpStorage = new List<WorkItemRevision>();
                                }
                            }
                        }
                    }

                    ctx.WorkItemRevisions.AddRange(tmpStorage);
                    ctx.SaveChanges();
                }

                Console.SetCursorPosition(0, initialPosition);
                Console.WriteLine(wiCount + "/" + totalNumberOfWorkItems);
                Console.WriteLine("Revision count: " + revCount);
                Console.WriteLine(stopwatch.Elapsed.TotalSeconds + " total seconds");
            }

            if (!options.AutoClose)
            {
                Console.WriteLine("push the <any> key to quit");
                Console.ReadKey();
            }
        }

        private static string GetChangedFields(Revision rev)
        {
            return string.Join("|", rev.Fields.Cast<Field>().Where(f => f.Value != f.OriginalValue).Select(f => f.ReferenceName));
        }
    }
}