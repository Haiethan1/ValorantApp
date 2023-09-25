using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
