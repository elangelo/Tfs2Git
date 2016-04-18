namespace Utils
{
    public class WorkItemFieldMapping
    {
        public string SourceFieldName { get; set; }
        public string TargetFieldName { get; set; }

        public WorkItemFieldAllowedValuesMapping[] WorkItemFieldAllowedValuesMapping { get; set; }
    }
}