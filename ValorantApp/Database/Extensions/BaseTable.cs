using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.Database.Extensions
{
    public class BaseTable : ITable
    {
        public bool CreateTables(string connectionString)
        {
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    ValorantUsersExtension.CreateTable(connection);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception when making tables. " + ex.Message);
                return false;
            }
            return true;
        }
    }
}
