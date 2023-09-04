using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations.Model;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValorantApp.Database.Tables;

namespace ValorantApp.Database.Extensions
{
    public class ValorantUsersExtension : BaseTable
    {
        private readonly string _ConnectionString;
        public ValorantUsersExtension(string connectionString)
        {
            _ConnectionString = connectionString;
        }
        public static void CreateTable(SQLiteConnection connection)
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS ValorantUsers (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    val_username TEXT NOT NULL,
                    val_tagname TEXT NOT NULL,
                    val_affinity TEXT NOT NULL,
                    val_puuid TEXT
                    
                )";
            using (var createTableCommand = new SQLiteCommand(createTableQuery, connection))
            {
                createTableCommand.ExecuteNonQuery();
            }
        }

        public bool insertRow(this ValorantUsers users)
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS ValorantUsers (
                    Id INTEGER PRIMARY KEY NOT NULL,
                    val_username TEXT NOT NULL,
                    val_tagname TEXT NOT NULL,
                    val_affinity TEXT NOT NULL,
                    val_puuid TEXT
                    
                )";
            using (var createTableCommand = new SQLiteCommand(createTableQuery, connection))
            {
                createTableCommand.ExecuteNonQuery();
            }
        }
    }
}
