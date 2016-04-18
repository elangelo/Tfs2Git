using System;

namespace Data
{
    public class WorkItemRevision
    {
        public int Id { get; set; }

        public int OriginalId { get; set; }

        public int NewId { get; set; }

        public int Revision { get; set; }

        public int RevisionCount { get; set; }

        public bool Migrated { get; set; }

        public DateTime Changed { get; set; }

        public string Kind { get; set; }

        public string ChangedFields { get; set; }

        public string Project { get; set; }
    }
}