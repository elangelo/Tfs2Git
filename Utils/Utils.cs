﻿using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.VersionControl.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Utils
{
    public static class Utils
    {
        private static string[] fieldsIDontCareAbout = new string[] { "Microsoft.VSTS.Build.IntegrationBuild", "Microsoft.VSTS.Build.FoundIn", "System.ChangedBy", "Microsoft.VSTS.Bild.IntegrationBuild", "Microsoft.VSTS.Common.ClosedBy", "Microsoft.VSTS.Common.ActivatedBy", "System.AssignedTo", "Microsoft.VSTS.CodeReview.AcceptedBy" };

        public static void CopyAreaAndIterationNodes(string sourceTfsTeamFoundationServerUrl, string sourceProjectName, string targetTfsTeamFoundationServerUrl, string targetProjectName, string AreaOfIterationPath)
        {
            var sourceTfs = GetTfsTeamProjectCollection(sourceTfsTeamFoundationServerUrl);
            var targetTfs = GetTfsTeamProjectCollection(targetTfsTeamFoundationServerUrl);

            var sourceProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            var targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            var targetCommonStructureService = targetTfs.GetService<ICommonStructureService>();

            NodeInfo targetAreaRootNodeInfo;
            NodeInfo targetIterationRootNodeInfo;

            if (string.IsNullOrEmpty(AreaOfIterationPath))
            {
                targetAreaRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectModelHierarchy").First();
                targetIterationRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectLifecycle").First();
            }
            else
            {
                var rootAreaRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectModelHierarchy").First();
                try
                {
                    targetAreaRootNodeInfo = targetCommonStructureService.GetNodeFromPath(rootAreaRootNodeInfo.Path + "\\" + AreaOfIterationPath);
                }
                catch
                {
                    //assume we get an exception becase the node doesn't exist
                    var targetAreaNodePath = targetCommonStructureService.CreateNode(AreaOfIterationPath, rootAreaRootNodeInfo.Uri);
                    targetAreaRootNodeInfo = targetCommonStructureService.GetNode(targetAreaNodePath);
                }

                var rootIterationRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectLifecycle").First();
                try
                {
                    targetIterationRootNodeInfo = targetCommonStructureService.GetNodeFromPath(rootIterationRootNodeInfo.Path + "\\" + AreaOfIterationPath);
                }
                catch
                {
                    var targetIterationNodePath = targetCommonStructureService.CreateNode(AreaOfIterationPath, rootIterationRootNodeInfo.Uri);
                    targetIterationRootNodeInfo = targetCommonStructureService.GetNode(targetIterationNodePath);
                }
            }

            //refresh, otherwise we can't find the new Areas/Iterations we just created
            targetProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            List<string> nodeUris = new List<string>();
            //copy areas

            var sourceAreaRootNodes = sourceProject.AreaRootNodes;
            //foreach (Node node in targetProject.AreaRootNodes)
            foreach (Node node in targetProject.FindNodeInSubTree(targetAreaRootNodeInfo.Name, Node.TreeType.Area))
            {
                nodeUris.Add(node.Uri.ToString());
            }

            if (nodeUris.Count > 0)
            {
                targetCommonStructureService.DeleteBranches(nodeUris.ToArray(), targetAreaRootNodeInfo.Uri.ToString());
            }

            CopyNodes(sourceAreaRootNodes, targetAreaRootNodeInfo, targetCommonStructureService);

            //copy iterations
            var sourceIterationRootNodes = sourceProject.IterationRootNodes;
            nodeUris = new List<string>();
            //foreach (Node node in targetProject.IterationRootNodes)
            foreach (Node node in targetProject.FindNodeInSubTree(targetIterationRootNodeInfo.Name, Node.TreeType.Iteration))
            {
                nodeUris.Add(node.Uri.ToString());
            }

            if (nodeUris.Count > 0)
            {
                targetCommonStructureService.DeleteBranches(nodeUris.ToArray(), targetIterationRootNodeInfo.Uri.ToString());
            }

            CopyNodes(sourceIterationRootNodes, targetIterationRootNodeInfo, targetCommonStructureService);
        }

        public static void CopyAreaNodes(string sourceTfsTeamFoundationServerUrl, string sourceProjectName, string targetTfsTeamFoundationServerUrl, string targetProjectName, string AreaPath)
        {
            var sourceTfs = GetTfsTeamProjectCollection(sourceTfsTeamFoundationServerUrl);
            var targetTfs = GetTfsTeamProjectCollection(targetTfsTeamFoundationServerUrl);

            var sourceProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            var targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            var targetCommonStructureService = targetTfs.GetService<ICommonStructureService>();

            NodeInfo targetAreaRootNodeInfo;

            if (string.IsNullOrEmpty(AreaPath))
            {
                targetAreaRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectModelHierarchy").First();
            }
            else
            {
                var rootAreaRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectModelHierarchy").First();
                try
                {
                    targetAreaRootNodeInfo = targetCommonStructureService.GetNodeFromPath(rootAreaRootNodeInfo.Path + "\\" + AreaPath);
                }
                catch
                {
                    //assume we get an exception becase the node doesn't exist
                    var targetAreaNodePath = targetCommonStructureService.CreateNode(AreaPath, rootAreaRootNodeInfo.Uri);
                    targetAreaRootNodeInfo = targetCommonStructureService.GetNode(targetAreaNodePath);
                }
            }

            //refresh, otherwise we can't find the new Areas/Iterations we just created
            targetProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            List<string> nodeUris = new List<string>();
            //copy areas
            var sourceAreaRootNodes = sourceProject.AreaRootNodes;
            foreach (Node node in targetProject.FindNodeInSubTree(targetAreaRootNodeInfo.Name, Node.TreeType.Area))
            {
                nodeUris.Add(node.Uri.ToString());
            }

            if (nodeUris.Count > 0)
            {
                targetCommonStructureService.DeleteBranches(nodeUris.ToArray(), targetAreaRootNodeInfo.Uri.ToString());
            }

            CopyNodes(sourceAreaRootNodes, targetAreaRootNodeInfo, targetCommonStructureService);
        }

        public static void CopyIterationNodes(string sourceTfsTeamFoundationServerUrl, string sourceProjectName, string targetTfsTeamFoundationServerUrl, string targetProjectName, string IterationPath)
        {
            var sourceTfs = GetTfsTeamProjectCollection(sourceTfsTeamFoundationServerUrl);
            var targetTfs = GetTfsTeamProjectCollection(targetTfsTeamFoundationServerUrl);

            var sourceProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            var targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            var targetCommonStructureService = targetTfs.GetService<ICommonStructureService>();

            NodeInfo targetAreaRootNodeInfo;
            NodeInfo targetIterationRootNodeInfo;

            if (string.IsNullOrEmpty(IterationPath))
            {
                targetIterationRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectLifecycle").First();
            }
            else
            {
                var rootIterationRootNodeInfo = targetCommonStructureService.ListStructures(targetProject.Uri.ToString()).Where(p => p.StructureType == "ProjectLifecycle").First();
                try
                {
                    targetIterationRootNodeInfo = targetCommonStructureService.GetNodeFromPath(rootIterationRootNodeInfo.Path + "\\" + IterationPath);
                }
                catch
                {
                    var targetIterationNodePath = targetCommonStructureService.CreateNode(IterationPath, rootIterationRootNodeInfo.Uri);
                    targetIterationRootNodeInfo = targetCommonStructureService.GetNode(targetIterationNodePath);
                }
            }

            //refresh, otherwise we can't find the new Areas/Iterations we just created
            targetProject = GetWorkItemStore(sourceTfsTeamFoundationServerUrl, false).Projects[sourceProjectName];
            targetProject = GetWorkItemStore(targetTfsTeamFoundationServerUrl, true).Projects[targetProjectName];

            //copy iterations
            var sourceIterationRootNodes = sourceProject.IterationRootNodes;
            var nodeUris = new List<string>();
            //foreach (Node node in targetProject.IterationRootNodes)
            foreach (Node node in targetProject.FindNodeInSubTree(targetIterationRootNodeInfo.Name, Node.TreeType.Iteration))
            {
                nodeUris.Add(node.Uri.ToString());
            }

            if (nodeUris.Count > 0)
            {
                targetCommonStructureService.DeleteBranches(nodeUris.ToArray(), targetIterationRootNodeInfo.Uri.ToString());
            }

            CopyNodes(sourceIterationRootNodes, targetIterationRootNodeInfo, targetCommonStructureService);
        }

        public static void CopyFields(Revision revision0, WorkItem targetWorkItem, Dictionary<int, int> areaNodeMap, Dictionary<int, int> iterationNodeMap, Project targetProject, Project sourceProject, WorkItemStoreMapping wism, bool newWorkItem, string areaRootPath, string iterationRootPath, bool addStatusChangesToHistory = false)
        {
            var workItemTypeMapping = wism.WorkItemTypeMapping.Where(w => w.SourceWorkItemType == revision0.WorkItem.Type.Name).First();

            var changeDate = string.Empty;
            var sourceReason = string.Empty;
            var targetReason = string.Empty;

            var sourceState = revision0.Fields["System.State"].Value;
            var targetState = revision0.Fields["System.State"].Value;

            bool stateChanged = false;
            //if (workItemTypeMapping.WorkItemFieldMappings.Where(f => f.SourceFieldName == "System.State").FirstOrDefault()?.WorkItemFieldAllowedValuesMapping.Where(wifv => wifv.SourceFieldValue == sourceState).FirstOrDefault()?.TargetFieldValue != targetState)
            if (revision0.Fields.Contains("Microsoft.VSTS.Common.StateChangeDate"))
            {
                if ((DateTime)revision0.Fields["Microsoft.VSTS.Common.StateChangeDate"]?.Value == (DateTime)revision0.Fields["System.ChangedDate"].Value)
                {
                    stateChanged = true;
                    changeDate = revision0.Fields["Microsoft.VSTS.Common.StateChangeDate"]?.Value.ToString();
                    sourceReason = revision0.Fields["System.Reason"]?.Value.ToString();
                    var reasonField = workItemTypeMapping.WorkItemFieldMappings.Where(f => f.SourceFieldName == "System.Reason").FirstOrDefault();
                    if (reasonField != null)
                    {
                        var targetReasonValue = reasonField.WorkItemFieldAllowedValuesMapping.Where(f => f.SourceFieldValue.ToString() == sourceReason).FirstOrDefault();
                        if (targetReasonValue != null)
                        {
                            targetReason = targetReasonValue.TargetFieldValue.ToString();
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine(revision0.WorkItem.Type);
            }

            foreach (Field field in revision0.Fields)
            {
                var query = workItemTypeMapping.WorkItemFieldMappings.Where(f => f.SourceFieldName == field.ReferenceName);
                var fieldMapping = query.FirstOrDefault();

                if (fieldMapping != null)
                {
                    var targetField = targetWorkItem.Fields[fieldMapping.TargetFieldName];
                    if (targetField != null)
                    {
                        if (targetField.IsEditable)
                        {
                            try
                            {
                                if (field.ReferenceName.Equals("System.AreaId", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (areaNodeMap.ContainsKey((int)field.Value))
                                    {
                                        targetField.Value = areaNodeMap[(int)field.Value];
                                    }
                                    else
                                    {
                                        targetField.Value = targetProject.Id;
                                    }
                                }
                                else if (field.ReferenceName.Equals("System.IterationId", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (iterationNodeMap.ContainsKey((int)field.Value))
                                    {
                                        targetField.Value = iterationNodeMap[(int)field.Value];
                                    }
                                    else
                                    {
                                        targetField.Value = targetProject.Id;
                                    }
                                }
                                else if (field.ReferenceName.Equals("System.CreatedDate", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (newWorkItem)
                                    {
                                        targetField.Value = field.Value;
                                    }
                                }
                                else if (field.ReferenceName.Equals("System.AreaPath", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (!string.IsNullOrEmpty(areaRootPath))
                                    {
                                        string tmpValue3 = field.Value.ToString();

                                        var r = new Regex($"{sourceProject.Name}");
                                        targetField.Value = r.Replace(tmpValue3, (m) => $"{targetProject.Name}\\{areaRootPath}");
                                    }
                                }
                                else if (field.ReferenceName.Equals("System.IterationPath", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    if (!string.IsNullOrEmpty(iterationRootPath))
                                    {
                                        string tmpValue3 = field.Value.ToString();

                                        var r = new Regex($"{sourceProject.Name}");
                                        targetField.Value = r.Replace(tmpValue3, (m) => $"{targetProject.Name}\\{iterationRootPath}");
                                    }
                                }
                                else if (field.Value != null && field.Value.GetType() == typeof(string))
                                {
                                    if (fieldMapping.WorkItemFieldAllowedValuesMapping != null && fieldMapping.WorkItemFieldAllowedValuesMapping.Length > 0 && !string.IsNullOrEmpty(field.Value.ToString()))
                                    {
                                        string tmpValue2 = field.Value.ToString();
                                        var targetValue = fieldMapping.WorkItemFieldAllowedValuesMapping.Where(p => p.SourceFieldValue.ToString() == tmpValue2).FirstOrDefault();
                                        if (targetValue != null)
                                        {
                                            targetField.Value = targetValue.TargetFieldValue;
                                        }
                                        else
                                        {
                                            throw new Exception($"missing a targetValue for sourceValue {tmpValue2}");
                                        }
                                    }
                                    else
                                    {
                                        string tmpValue = field.Value.ToString();
                                        //replace old projectname with new projectname
                                        targetField.Value = tmpValue.Replace(sourceProject.Name, targetProject.Name);
                                    }
                                }
                                else
                                {
                                    targetField.Value = field.Value;
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine(ex);
                                Console.ResetColor();
                            }
                        }
                    }
                }
                else
                {
                    Trace.WriteLine(string.Format("Field {0} not found in mapping!!!!", field.ReferenceName));
                }
            }

            if (revision0.Attachments != null && targetWorkItem.Attachments != null)
            {
                //attachments to add...
                List<Attachment> toBeAdded = revision0.Attachments.Cast<Attachment>().Except(targetWorkItem.Attachments.Cast<Attachment>(), new AttachmentComparer()).ToList();

                List<string> filenames = new List<string>();

                foreach (var attachment in toBeAdded)
                {
                    string attachmentName = attachment.Name;

                    if (filenames.Contains(attachment.Name))
                    {
                        attachmentName += GetRandomSuffix();
                    }

                    filenames.Add(attachmentName);

                    var attachementDirectory = System.IO.Path.GetTempPath();
                    var attachementFilePath = Path.Combine(attachementDirectory, attachmentName);

                    using (var webClient = new WebClient())
                    {
                        webClient.UseDefaultCredentials = true;
                        webClient.DownloadFile(attachment.Uri, attachementFilePath);
                    }

                    targetWorkItem.Attachments.Add(new Attachment(attachementFilePath, attachment.Comment));
                }
            }

            //attachements are removed!!!
            List<Attachment> toBeRemoved = targetWorkItem.Attachments.Cast<Attachment>().Except(revision0.Attachments.Cast<Attachment>(), new AttachmentComparer()).ToList();
            foreach (var attachment in toBeRemoved)
            {
                targetWorkItem.Attachments.Remove(attachment);
            }

            if (revision0.Fields.Contains("Microsoft.VSTS.Common.StateChangeDate") && stateChanged && addStatusChangesToHistory)
            {
                var mappedon = (targetReason != null ? $" mapped on '{targetReason}'" : "");
                var historyMsg = $"{changeDate}: State changed to '{sourceState}', Reason was '{sourceReason}'{mappedon}";
                targetWorkItem.Fields["System.History"].Value += historyMsg;
            }

            if (newWorkItem)
            {
                var historyMsg = $"***Tfs WorkItem Migration Process*** {Environment.NewLine}Copied from test case with id {revision0.WorkItem.Id}";
                targetWorkItem.Fields["System.History"].Value += historyMsg;
            }
        }

        public static void CopySubStuff(QueryFolder sourceQueryFolder, QueryFolder targetQueryFolder)
        {
            var workItemStore = sourceQueryFolder.Project.Store;

            var variables = new Dictionary<string, string>
            {
                { "project", sourceQueryFolder.Project.Name }
            };
            foreach (var sourceQueryFolderSubItem in sourceQueryFolder)
            {
                if (sourceQueryFolderSubItem is QueryDefinition sourceQueryDefinition)
                {
                    //this is a query definition
                    //only try to migrate query if query is valid.
                    try
                    {
                        var query = new Query(workItemStore, sourceQueryDefinition.QueryText, variables);

                        if (sourceQueryDefinition.QueryType == QueryType.List)
                        {
                            query.RunQuery();
                        }
                        else
                        {
                            query.RunLinkQuery();
                        }
                    }
                    catch (Microsoft.TeamFoundation.WorkItemTracking.Client.ValidationException)
                    {
                        //query was not valid: iteration path no longer exists or something alike.
                        continue;
                    }
                    catch
                    {
                        //crash
                        throw;
                    }

                    string queryText = sourceQueryDefinition.QueryText;
                    queryText = queryText.Replace(sourceQueryDefinition.Project.Name, targetQueryFolder.Project.Name);
                    if (targetQueryFolder.Contains(sourceQueryDefinition.Name))
                    {
                        targetQueryFolder[sourceQueryDefinition.Name].Delete();
                    }

                    targetQueryFolder.Add(new QueryDefinition(sourceQueryDefinition.Name, queryText));
                }
                else
                {
                    if (sourceQueryFolderSubItem is QueryFolder sourceSubQueryFolder)
                    {
                        //this is a query folder
                        var targetSubQueryFolder = new QueryFolder(sourceSubQueryFolder.Name);
                        if (targetQueryFolder.Contains(sourceSubQueryFolder.Name))
                        {
                            targetQueryFolder[targetSubQueryFolder.Name].Delete();
                        }

                        targetQueryFolder.Add(targetSubQueryFolder);
                        CopySubStuff(sourceSubQueryFolder, targetSubQueryFolder);
                    }
                }
            }
        }

        public static List<WorkItemTypeMapping> CreateWorkItemTypeMapping(WorkItemTypeCollection workItemTypes1, WorkItemTypeCollection workItemTypes2, Dictionary<string, string> manualMappings)
        {
            var result = new List<WorkItemTypeMapping>();

            foreach (WorkItemType sourceWorkItemType in workItemTypes1)
            {
                var query = workItemTypes2.Cast<WorkItemType>().Where(p => p.Name == sourceWorkItemType.Name);
                var targetWorkItemType = query.FirstOrDefault();
                if (targetWorkItemType != null)
                {
                    result.Add(new WorkItemTypeMapping()
                    {
                        SourceWorkItemType = sourceWorkItemType.Name,
                        TargetWorkItemType = targetWorkItemType.Name,
                        WorkItemFieldMappings = CreateFieldMapping(sourceWorkItemType, targetWorkItemType).ToArray()
                    });
                }
                else if (manualMappings.ContainsKey(sourceWorkItemType.Name))
                {
                    Trace.WriteLine("adding a manual mapping");

                    targetWorkItemType = workItemTypes2.Cast<WorkItemType>().Where(p => p.Name == manualMappings[sourceWorkItemType.Name]).FirstOrDefault();
                    if (targetWorkItemType != null)
                    {
                        result.Add(new WorkItemTypeMapping()
                        {
                            SourceWorkItemType = sourceWorkItemType.Name,
                            TargetWorkItemType = targetWorkItemType.Name,
                            WorkItemFieldMappings = CreateFieldMapping(sourceWorkItemType, targetWorkItemType).ToArray()
                        });
                    }
                    else
                    {
                        Trace.TraceError($"The target WorkItemType manually mapped with {sourceWorkItemType.Name}:{manualMappings[sourceWorkItemType.Name]} was not found in the TargetProject");
                    }
                }
                else
                {
                    Trace.WriteLine($"WorkItemType {sourceWorkItemType.Name} was not found in targetproject!");
                }
            }

            return result;
        }

        public static Dictionary<string, string> GetAllAuthors(string tfsTeamFoundationServerUrl, string projectName)
        {
            var authors = new Dictionary<string, string>();

            var tfsProjectCollection = GetTfsTeamProjectCollection(tfsTeamFoundationServerUrl);
            var vcs = tfsProjectCollection.GetService<VersionControlServer>();
            var changesets = vcs.QueryHistory($"$/{projectName}", RecursionType.Full);
            foreach (var changeset in changesets)
            {
                var author = changeset.CommitterDisplayName;
                if (!authors.ContainsKey(author))
                {
                    authors.Add(author, changeset.Committer);
                }
            }

            return authors;
        }

        public static Dictionary<int, int> GetNodeMap(NodeCollection sourceRootNodes, NodeCollection targetRootNodes, bool mapEverythingToRoot = false, int rootNode = 0)
        {
            if (mapEverythingToRoot && rootNode == 0)
            {
                rootNode = targetRootNodes[0].ParentNode.Id;
            }
            var iterationNodeMap = new Dictionary<int, int>();

            foreach (Node node in sourceRootNodes)
            {
                if (mapEverythingToRoot)
                {
                    iterationNodeMap.Add(node.Id, rootNode);
                    if (node.HasChildNodes)
                    {
                        var r = Utils.GetNodeMap(node.ChildNodes, targetRootNodes, true, rootNode);
                        foreach (var kvp in r)
                        {
                            iterationNodeMap.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
                else
                {
                    var targetNode = targetRootNodes.Cast<Node>().Where(n => n.Name == node.Name).First();

                    iterationNodeMap.Add(node.Id, targetNode.Id);

                    if (node.HasChildNodes)
                    {
                        var r = Utils.GetNodeMap(node.ChildNodes, targetNode.ChildNodes, false, 0);
                        foreach (var kvp in r)
                        {
                            iterationNodeMap.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
            }
            return iterationNodeMap;
        }

        public static Tuple<Project, QueryFolder> GetShareQueryFolder(string targetTfsCollection, string targetTfsProject)
        {
            var targetTfsTeamProjectCollection = GetTfsTeamProjectCollection(targetTfsCollection);

            WorkItemStore targetWorkItemStore = targetTfsTeamProjectCollection.GetService<WorkItemStore>();

            Project targetProject = targetWorkItemStore.Projects[targetTfsProject];

            QueryFolder targetQueryFolder = null;
            var targetQueryHierarchy = targetProject.QueryHierarchy;
            foreach (var targetSubItem in targetQueryHierarchy)
            {
                targetQueryFolder = targetSubItem as QueryFolder;
                if (targetQueryFolder != null && targetQueryFolder.Name == "Shared Queries")
                {
                    break;
                }
            }
            return new Tuple<Project, QueryFolder>(targetProject, targetQueryFolder);
        }

        public static WorkItemCollection GetWorkItems(WorkItemStore workItemStore, string projectName)
        {
            return workItemStore.Query($"SELECT * FROM workitems WHERE [System.TeamProject] = '{projectName}' and [System.WorkItemType] <> 'Test Plan' and [System.WorkItemType] <> 'Test Suite'");
        }

        public static WorkItemStore GetWorkItemStore(string tfsTeamFoundationServerUrl, bool activateBypassRules)
        {
            TfsTeamProjectCollection tfsTeamProjectCollection = GetTfsTeamProjectCollection(tfsTeamFoundationServerUrl);

            if (activateBypassRules)
            {
                return new WorkItemStore(tfsTeamProjectCollection, WorkItemStoreFlags.BypassRules);
            }
            else
            {
                return new WorkItemStore(tfsTeamProjectCollection, WorkItemStoreFlags.None);
            }
        }

        public static WorkItemStoreMapping ReadWorkItemMappingFile(string path)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(WorkItemStoreMapping), new Type[] { typeof(WorkItemFieldMapping), typeof(WorkItemTypeMapping) });
            WorkItemStoreMapping result;
            using (var sr = new StreamReader(path))
            {
                result = (WorkItemStoreMapping)serializer.Deserialize(sr);
            }
            return result;
        }

        internal static TfsTeamProjectCollection GetTfsTeamProjectCollection(string tfsTeamFoundationServerUrl)
        {
            var tfsTeamProjectCollection = new TfsTeamProjectCollection(new Uri(tfsTeamFoundationServerUrl));

            tfsTeamProjectCollection.EnsureAuthenticated();
            return tfsTeamProjectCollection;
        }

        private static void CopyNodes(NodeCollection sourceNodes, NodeInfo targetRootNode, ICommonStructureService css)
        {
            foreach (Node sourceNode in sourceNodes)
            {
                string newNodePath = css.CreateNode(sourceNode.Name, targetRootNode.Uri);
                var targetNode = css.GetNode(newNodePath);

                if (sourceNode.HasChildNodes)
                {
                    CopyNodes(sourceNode.ChildNodes, targetNode, css);
                }
            }
        }

        private static List<WorkItemFieldMapping> CreateFieldMapping(WorkItemType sourceWorkItemType, WorkItemType targetWorkItemType)
        {
            var result = new List<WorkItemFieldMapping>();
            foreach (FieldDefinition sourceFieldDefinition in sourceWorkItemType.FieldDefinitions)
            {
                var query = targetWorkItemType.FieldDefinitions.Cast<FieldDefinition>().Where(p => p.ReferenceName == sourceFieldDefinition.ReferenceName);
                var targetFieldDefinition = query.FirstOrDefault();
                if (targetFieldDefinition != null)
                {
                    List<WorkItemFieldAllowedValuesMapping> valueMapping = new List<WorkItemFieldAllowedValuesMapping>();
                    if (!fieldsIDontCareAbout.Contains(targetFieldDefinition.ReferenceName))
                    {
                        foreach (var allowedValue in sourceFieldDefinition.AllowedValues)
                        {
                            if (targetFieldDefinition.AllowedValues.Contains(allowedValue.ToString()))
                            {
                                valueMapping.Add(new WorkItemFieldAllowedValuesMapping() { SourceFieldValue = allowedValue, TargetFieldValue = allowedValue });
                            }
                            else
                            {
                                valueMapping.Add(new WorkItemFieldAllowedValuesMapping() { SourceFieldValue = allowedValue, TargetFieldValue = string.Join("|", targetFieldDefinition.AllowedValues.Cast<string>()) });
                            }
                        }
                    }

                    result.Add(new WorkItemFieldMapping()
                    {
                        SourceFieldName = sourceFieldDefinition.ReferenceName,
                        TargetFieldName = targetFieldDefinition.ReferenceName,
                        WorkItemFieldAllowedValuesMapping = valueMapping.ToArray()
                    });
                }
                else
                {
                    Trace.WriteLine($"Field {sourceFieldDefinition.ReferenceName} was not found on TargetWorkItemType {targetWorkItemType}");
                }
            }

            return result;
        }

        private static string GetRandomSuffix()
        {
            return Guid.NewGuid().ToString();
        }

        private class AttachmentComparer : IEqualityComparer<Attachment>
        {
            public bool Equals(Attachment x, Attachment y)
            {
                //throw new NotImplementedException();
                //return x.FileGuid == y.FileGuid;
                return x.Length == y.Length && x.Name == y.Name && x.Comment == y.Comment;// && x.AttachedTime == y.AttachedTime;
            }

            public int GetHashCode(Attachment obj)
            {
                return obj.Name.GetHashCode();
            }
        }
    }
}

public class FieldDefinitionComparer : IEqualityComparer<FieldDefinition>
{
    public bool Equals(FieldDefinition x, FieldDefinition y)
    {
        return x.ReferenceName == y.ReferenceName;
    }

    public int GetHashCode(FieldDefinition obj)
    {
        return obj.ReferenceName.GetHashCode();
    }
}