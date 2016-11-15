using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.TestManagement.Client;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

//code mostly copied from TotalTFSMigration, just added code to migrate query based test plans


namespace Utils
{
    public class TestPlanMigration
    {
        private ITestManagementTeamProject sourceTestMgmtProj;
        private ITestManagementTeamProject targetTestMgmtProj;
        public Dictionary<int, int> WorkItemMap;
        public Dictionary<int, int> AreaMap;
        public Dictionary<int, int> IterationMap;

        private String SourceProjectName;
        private String TargetProjectName;

        public Dictionary<string, string> AreaMapTitles { get; private set; }
        public Dictionary<string, string> IterationMapTitle { get; private set; }

        public TestPlanMigration(string sourceTfsUrl, string targetTfsUrl, string sourceProjectName, string targetProjectName, Dictionary<int, int> workItemMap)
        {
            var sourceTfs = Utils.GetTfsTeamProjectCollection(sourceTfsUrl);
            var destinationTfs = Utils.GetTfsTeamProjectCollection(targetTfsUrl);

            this.SourceProjectName = sourceProjectName;
            this.TargetProjectName = targetProjectName;

            this.sourceTestMgmtProj = GetProject(sourceTfs, sourceProjectName);
            this.targetTestMgmtProj = GetProject(destinationTfs, targetProjectName);
            this.WorkItemMap = workItemMap;

            var targetWorkitemStore = Utils.GetWorkItemStore(targetTfsUrl, true);
            var sourceWorkitemStore = Utils.GetWorkItemStore(sourceTfsUrl, false);

            var targetProject = targetWorkitemStore.Projects[targetProjectName];
            var sourceProject = sourceWorkitemStore.Projects[sourceProjectName];

            this.AreaMap = Utils.GetNodeMap(sourceProject.AreaRootNodes, targetProject.AreaRootNodes);
            this.AreaMapTitles = new Dictionary<string, string>();
            foreach (var kvp in this.AreaMap)
            {
                this.AreaMapTitles.Add(sourceProject.FindNodeInSubTree(kvp.Key).Path, targetProject.FindNodeInSubTree(kvp.Value).Path);
            }

            this.IterationMap = Utils.GetNodeMap(sourceProject.IterationRootNodes, targetProject.IterationRootNodes);
            this.IterationMapTitle = new Dictionary<string, string>();
            foreach (var kvp in this.IterationMap)
            {
                this.IterationMapTitle.Add(sourceProject.FindNodeInSubTree(kvp.Key).Path, targetProject.FindNodeInSubTree(kvp.Value).Path);
            }
        }

        private ITestManagementTeamProject GetProject(TfsTeamProjectCollection tfs, string project)
        {
            ITestManagementService tms = tfs.GetService<ITestManagementService>();

            return tms.GetTeamProject(project);
        }

        public void CopyTestPlans()
        {
            int planCount = sourceTestMgmtProj.TestPlans.Query("Select * From TestPlan").Count;
            //delete Test Plans if any existing test plans.
            //foreach (ITestPlan destinationplan in destinationproj.TestPlans.Query("Select * From TestPlan"))
            //{
            //    System.Diagnostics.Debug.WriteLine("Deleting Plan - {0} : {1}", destinationplan.Id, destinationplan.Name);
            //    destinationplan.Delete(DeleteAction.ForceDeletion); ;
            //}

            foreach (ITestPlan sourceplan in sourceTestMgmtProj.TestPlans.Query("Select * From TestPlan"))
            {
                System.Diagnostics.Debug.WriteLine($"Plan - {sourceplan.Id} : {sourceplan.Name}");

                ITestPlan destinationplan = targetTestMgmtProj.TestPlans.Create();

                destinationplan.Name = sourceplan.Name;
                destinationplan.Description = sourceplan.Description;
                destinationplan.StartDate = sourceplan.StartDate;
                destinationplan.EndDate = sourceplan.EndDate;
                destinationplan.State = sourceplan.State;
                destinationplan.Save();

                //drill down to root test suites.
                if (sourceplan.RootSuite != null && sourceplan.RootSuite.Entries.Count > 0)
                {
                    CopyTestSuites(sourceplan, destinationplan);
                }

                destinationplan.Save();
            }
        }

        //Copy all Test suites from source plan to destination plan.
        private void CopyTestSuites(ITestPlan sourceplan, ITestPlan destinationplan)
        {
            ITestSuiteEntryCollection suites = sourceplan.RootSuite.Entries;
            CopyTestCases(sourceplan.RootSuite, destinationplan.RootSuite);

            foreach (ITestSuiteEntry suite_entry in suites)
            {
                IStaticTestSuite suite = suite_entry.TestSuite as IStaticTestSuite;
                if (suite != null)
                {
                    IStaticTestSuite newSuite = targetTestMgmtProj.TestSuites.CreateStatic();
                    newSuite.Title = suite.Title;
                    destinationplan.RootSuite.Entries.Add(newSuite);
                    destinationplan.Save();

                    CopyTestCases(suite, newSuite);
                    if (suite.Entries.Count > 0)
                        CopySubTestSuites(suite, newSuite);
                }
                else
                {
                    IDynamicTestSuite dynamicSuite = suite_entry.TestSuite as IDynamicTestSuite;
                    if (dynamicSuite != null)
                    {
                        IDynamicTestSuite newDynamicSuit = targetTestMgmtProj.TestSuites.CreateDynamic();
                        newDynamicSuit.Title = dynamicSuite.Title;
                        //newDynamicSuit.Query = dynamicSuite.Query;

                        var text = ReplaceAreaPath(dynamicSuite.Query.QueryText);
                        text = ReplaceIterationPath(text);

                        var newQuery = targetTestMgmtProj.CreateTestQuery(text);

                        newDynamicSuit.Query = newQuery;

                        destinationplan.RootSuite.Entries.Add(newDynamicSuit);
                        destinationplan.Save();
                    }
                }
            }
        }

        private string ReplaceAreaPath(string text)
        {
            Regex r = new Regex(@"(\[System.AreaPath])[ ]*([Uu][Nn][Dd][Ee][Rr]|[Nn][Oo][Tt] [Uu][Nn][Dd][Ee][Rr]|=|<>|[Ii][Nn])[ ]*'([^\']*)'");
            return r.Replace(text, m =>
            {
                var areaPath = m.Groups[3].Value;

                if (AreaMapTitles.ContainsKey(areaPath))
                {
                    areaPath = AreaMapTitles[areaPath];
                }
                else if (areaPath.Equals(SourceProjectName, StringComparison.InvariantCultureIgnoreCase))
                {
                    areaPath = TargetProjectName;
                }
                else
                {
                    Console.WriteLine("gvd");
                }

                return $"{m.Groups[1]} {m.Groups[2]} '{areaPath}'";


                //areaPath = areaPath.Replace(this.SourceProjectName, this.TargetProjectName);
                //foreach (var split in areaPath.Split('\\'))
                //{
                //    if (this.AreaMapTitles.ContainsKey(split))
                //    {
                //        areaPath = areaPath.Replace(split, this.AreaMapTitles[split]);
                //    }
                //}

                //return $"{m.Groups[1]} {m.Groups[2]} '{areaPath}'";
            });
        }

        private string ReplaceIterationPath(string text)
        {
            Regex r = new Regex(@"(\[System.IterationPath])[ ]*([Uu][Nn][Dd][Ee][Rr]|[Nn][Oo][Tt] [Uu][Nn][Dd][Ee][Rr]|=|<>|[Ii][Nn])[ ]*'([^\']*)'");
            return r.Replace(text, m =>
                {
                    var iterationPath = m.Groups[3].Value;

                    if (IterationMapTitle.ContainsKey(iterationPath))
                    {
                        iterationPath = IterationMapTitle[iterationPath];
                    }
                    else if (iterationPath.Equals(SourceProjectName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        iterationPath = TargetProjectName;
                    }
                    else
                    {
                        Console.WriteLine("gvd");
                    }

                    return $"{m.Groups[1]} {m.Groups[2]} '{iterationPath}'";

                    //iterationPath = iterationPath.Replace(this.SourceProjectName, this.TargetProjectName);
                    //foreach (var split in iterationPath.Split('\\'))
                    //{
                    //    if (this.IterationMapTitle.ContainsKey(split))
                    //    {
                    //        iterationPath = iterationPath.Replace(split, this.IterationMapTitle[split]);
                    //    }
                    //}
                    //return $"{m.Groups[1]} {m.Groups[2]} '{iterationPath}'";
                }
            );
        }

        //Drill down and Copy all subTest suites from source root test suite to destination plan's root test suites.
        private void CopySubTestSuites(IStaticTestSuite parentsourceSuite, IStaticTestSuite parentdestinationSuite)
        {
            ITestSuiteEntryCollection suitcollection = parentsourceSuite.Entries;
            foreach (ITestSuiteEntry suite_entry in suitcollection)
            {
                IStaticTestSuite suite = suite_entry.TestSuite as IStaticTestSuite;
                if (suite != null)
                {
                    IStaticTestSuite subSuite = targetTestMgmtProj.TestSuites.CreateStatic();
                    subSuite.Title = suite.Title;
                    parentdestinationSuite.Entries.Add(subSuite);

                    CopyTestCases(suite, subSuite);

                    if (suite.Entries.Count > 0)
                        CopySubTestSuites(suite, subSuite);
                }
                else
                {
                    IDynamicTestSuite dynamicSuite = suite_entry.TestSuite as IDynamicTestSuite;
                    if (dynamicSuite != null)
                    {
                        IDynamicTestSuite newDynamicSuit = targetTestMgmtProj.TestSuites.CreateDynamic();
                        newDynamicSuit.Title = dynamicSuite.Title;

                        var text = ReplaceAreaPath(dynamicSuite.Query.QueryText);
                        text = ReplaceIterationPath(text);

                        var newQuery = targetTestMgmtProj.CreateTestQuery(text);

                        newDynamicSuit.Query = newQuery;
                    }
                }
            }
        }

        //Copy all subTest suites from source root test suite to destination plan's root test suites.
        private void CopyTestCases(IStaticTestSuite sourcesuite, IStaticTestSuite destinationsuite)
        {
            ITestSuiteEntryCollection suiteentrys = sourcesuite.TestCases;

            foreach (ITestSuiteEntry testcase in suiteentrys)
            {
                try
                {   //check whether testcase exists in new work items(closed work items may not be created again).
                    if (!WorkItemMap.ContainsKey(testcase.TestCase.WorkItem.Id))
                    {
                        continue;
                    }

                    int newWorkItemID = (int)WorkItemMap[testcase.TestCase.WorkItem.Id];
                    ITestCase tc = targetTestMgmtProj.TestCases.Find(newWorkItemID);
                    destinationsuite.Entries.Add(tc);

                    bool updateTestCase = false;
                    TestActionCollection testActionCollection = tc.Actions;
                    foreach (var item in testActionCollection)
                    {
                        var sharedStepRef = item as ISharedStepReference;
                        if (sharedStepRef != null)
                        {
                            int newSharedStepId = (int)WorkItemMap[sharedStepRef.SharedStepId];
                            //GetNewSharedStepId(testCase.Id, sharedStepRef.SharedStepId);
                            if (0 != newSharedStepId)
                            {
                                sharedStepRef.SharedStepId = newSharedStepId;
                                updateTestCase = true;
                            }
                        }
                    }
                    if (updateTestCase)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Test case with Id: {tc.Id} updated");
                        tc.Save();
                    }
                }
                catch (Exception)
                {
                    Console.WriteLine($"Error retrieving Test case {testcase.TestCase.WorkItem.Id}: {testcase.Title}");
                }
            }
        }
    }
}