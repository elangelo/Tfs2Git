namespace Data.Migrations
{
    using System.Data.Entity.Migrations;

    internal sealed class Configuration : DbMigrationsConfiguration<Data.VSOMigrDB>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "Data.VSOMigrDB";
        }

        protected override void Seed(Data.VSOMigrDB context)
        {

        }
    }
}