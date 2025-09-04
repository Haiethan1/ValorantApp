using System.Configuration;
using System.Diagnostics;

namespace ValorantApp.Database.Extensions
{
    public abstract class BaseTable : ITable
    {
        protected static readonly string connectionString = ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
        protected static readonly string sqlConnectionString = ConfigurationManager.ConnectionStrings[Debugger.IsAttached ? "DatabaseDev" : "DatabaseProd"].ConnectionString;

        public BaseTable()
        {
        }

        public string CreateTable()
        {
            throw new NotImplementedException();
        }
    }
}
