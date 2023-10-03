using System.Configuration;

namespace ValorantApp.Database.Extensions
{
    public abstract class BaseTable : ITable
    {
        protected static readonly string connectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;

        public BaseTable()
        {
        }

        public string CreateTable()
        {
            throw new NotImplementedException();
        }
    }
}
