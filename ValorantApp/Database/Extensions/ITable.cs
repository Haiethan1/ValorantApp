using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.Database.Extensions
{
    public interface ITable
    {
        bool CreateTables(string connectionString);
    }
}
