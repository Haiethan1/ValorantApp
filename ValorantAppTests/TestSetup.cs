using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using ValorantApp.Database.Extensions;

namespace ValorantAppTests
{
    /// <summary>
    /// Fails closed: no test in this assembly can reach SQL Server (prod or dev) or the real SQLite file.
    /// SQL Server writes are disabled and SQLite points at a throwaway temp file with the schema created.
    /// </summary>
    [TestClass]
    public static class TestSetup
    {
        private static string? _dbPath;

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext _)
        {
            DbSettings.SqlServerEnabled = false;

            _dbPath = Path.Combine(Path.GetTempPath(), $"valorantapp-tests-{Guid.NewGuid():N}.db");
            DbSettings.Sqlite = $"Data Source={_dbPath}";
            Assert.IsTrue(ITable.CreateTables(DbSettings.Sqlite), "Could not create test SQLite schema");
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                if (_dbPath is not null) File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // Best effort: temp file, OS will clean it up.
            }
        }
    }
}
