using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ValorantApp.Database.Extensions
{
    public interface ITable
    {
        static bool CreateTables(string connectionString)
        {
            try
            {
                StringBuilder createTableQuery = new StringBuilder();
                using var connection = new SQLiteConnection(connectionString);
                connection.Open();

                createTableQuery.AppendLine(ValorantUsersExtension.CreateTable());
                createTableQuery.AppendLine(MatchStatsExtension.CreateTable());


                using var createTableCommand = new SQLiteCommand(createTableQuery.ToString(), connection);
                createTableCommand.ExecuteNonQuery();

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception when making tables. " + ex.Message);
                return false;
            }
            return true;
        }

        // find a way to make this static or force all other tabls to make this method
        string CreateTable();

        // TODO: refactor to add interface methods
        // Want a insert / update row, add cache, refresh cache
    }
}
