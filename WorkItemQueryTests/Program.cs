using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkItemQueryTests
{
    class Program
    {
        static void Main(string[] args)
        {
            TfsTeamProjectCollection tpc = new TfsTeamProjectCollection(
      new Uri("http://tfsserver:8080/tfs/TestCollection"));
            WorkItemStore workItemStore = (WorkItemStore)tpc.GetService(typeof(WorkItemStore));

            string wiql = "Select * From WorkItems Where [Work Item Type] = 'Test Case' and [System.TeamProject] = 'adam asf_test' ORDER BY [System.Tags]";
            var queryResults = workItemStore.Query(wiql);

            Console.WriteLine(queryResults.Count);

            Console.WriteLine("press the <any> key");
            Console.ReadKey();

        }
    }
}
