namespace Data.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class addtestplanmapping : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.TestPlanMappings",
                c => new
                {
                    Id = c.Int(nullable: false, identity: true),
                    originalID = c.Int(nullable: false),
                    targetID = c.Int(nullable: false),
                })
                .PrimaryKey(t => t.Id);
        }

        public override void Down()
        {
            DropTable("dbo.TestPlanMappings");
        }
    }
}