using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using ValorantApp.Database.Extensions;
using ValorantApp.Database.Tables;

namespace ValorantAppTests
{
    /// <summary>
    /// Dual writes against a real SQL Server (the Testcontainers instance from <see cref="SqlServerContainer"/>, never
    /// the NAS). Each test enables SQL Server only inside its scope and starts from empty tables.
    /// </summary>
    [TestClass]
    [TestCategory(SqlServerContainer.Category)]
    [DoNotParallelize]
    public class SqlServerDualWriteTests
    {
        private IDisposable? _sqlServer;

        [TestInitialize]
        public void Init()
        {
            _sqlServer = SqlServerContainer.Enable();
            SqlServerContainer.ClearAllTables();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _sqlServer?.Dispose();
            Assert.IsFalse(DbSettings.SqlServerEnabled, "SQL Server must be disabled again after each test");
        }

        [TestMethod]
        public void SqlServer_IsTheLocalContainer()
        {
            Assert.IsTrue(DbSettings.SqlServerEnabled);
            Assert.AreEqual(SqlServerContainer.ConnectionString, DbSettings.SqlServer);
            Assert.AreEqual("Valorant", SqlServerContainer.Scalar("SELECT DB_NAME()"));
        }

        [TestMethod]
        public void ValorantUsers_InsertAndDelete_AreMirroredToSqlServer()
        {
            var user = new ValorantUsers("sqltester", "0001", "na", "puuid-sqlserver-users", 123456789012345678UL);

            try
            {
                Assert.IsTrue(ValorantUsersExtension.InsertRow(user));

                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual("sqltester", SqlServerContainer.Scalar("SELECT val_username FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
                Assert.AreEqual(123456789012345678L, SqlServerContainer.Scalar("SELECT disc_id FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));

                // Inserting the same user again must not duplicate the SQL Server row.
                ValorantUsersExtension.InsertRow(user);
                Assert.AreEqual(1, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));

                Assert.IsTrue(ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id));
                Assert.AreEqual(0, SqlServerContainer.Scalar("SELECT COUNT(*) FROM lu_valorant_users WHERE val_puuid = @p", ("@p", user.Val_puuid)));
            }
            finally
            {
                ValorantUsersExtension.DeleteRow(user.Val_puuid, user.Disc_id);
            }
        }
    }
}
