using Microsoft.Data.Sqlite;
using System.Text;

namespace ValorantApp.Database.Extensions
{
    public interface ITable
    {
        static bool CreateTables(string connectionString)
        {
            try
            {
                StringBuilder createTableQuery = new StringBuilder();
                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                createTableQuery.AppendLine(ValorantUsersExtension.CreateTable());
                createTableQuery.AppendLine(MatchStatsExtension.CreateTable());
                createTableQuery.AppendLine(MatchesExtension.CreateTable());


                using var createTableCommand = new SqliteCommand(createTableQuery.ToString(), connection);
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
