namespace ValorantApp.Database.Extensions
{
    public abstract class BaseTable : ITable
    {
        protected static string connectionString => DbSettings.Sqlite;
        protected static string sqlConnectionString => DbSettings.SqlServer;
        protected static bool SqlServerEnabled => DbSettings.SqlServerEnabled;

        public BaseTable()
        {
        }

        public string CreateTable()
        {
            throw new NotImplementedException();
        }
    }
}
