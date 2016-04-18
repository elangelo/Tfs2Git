namespace Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddProjectNameColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.WorkItemRevisions", "Project", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.WorkItemRevisions", "Project");
        }
    }
}