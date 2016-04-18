namespace Utils
{
    public class WorkItemTypeMapping
    {
        public string SourceWorkItemType { get; set; }
        public string TargetWorkItemType { get; set; }

        public WorkItemFieldMapping[] WorkItemFieldMappings { get; set; }
    }
}