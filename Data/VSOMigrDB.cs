namespace Data
{
    using System.Data.Entity;

    public class VSOMigrDB : DbContext
    {
        // Your context has been configured to use a 'VSOMigrDB' connection string from your application's
        // configuration file (App.config or Web.config). By default, this connection string targets the
        // 'Data.VSOMigrDB' database on your LocalDb instance.
        //
        // If you wish to target a different database and/or database provider, modify the 'VSOMigrDB'
        // connection string in the application configuration file.
        public VSOMigrDB()
            : base("name=VSOMigrDB")
        {
        }

        public VSOMigrDB(string connectionString)
            : base(connectionString)
        {
        }

        public virtual DbSet<WorkItemRevision> WorkItemRevisions { get; set; }

        public virtual DbSet<TestPlanMapping> TestPlanMappings { get; set; }
    }
}