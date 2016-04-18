namespace Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.WorkItemRevisions",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    OriginalId = c.Int(nullable: false),
                    NewId = c.Int(nullable: false),
                    Revision = c.Int(nullable: false),
                    RevisionCount = c.Int(nullable: false),
                    Migrated = c.Boolean(nullable: false),
                    Changed = c.DateTime(nullable: false),
                    Kind = c.String(),
                    ChangedFields = c.String(),
                })
                .PrimaryKey(t => t.Id);
        }

        public override void Down()
        {
            DropTable("dbo.WorkItemRevisions");
        }
    }
}