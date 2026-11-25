using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Testcontainers.MsSql;
using ValorantApp.Database.Extensions;

namespace ValorantAppTests
{
    /// <summary>
    /// A throwaway SQL Server 2022 (same image as the NAS) in Docker, started once per test run and shared by every
    /// [TestCategory("SqlServer")] test. The schema comes from db/sqlserver/*.sql, applied in filename order.
    ///
    /// Safety: this is the ONLY way a test may enable SQL Server. <see cref="Enable"/> refuses any connection string
    /// whose server is not on this machine, and the returned scope turns SQL Server back off, so the rest of the suite
    /// stays fail-closed (see TestSetup and docs/testing.md "Safety").
    ///
    /// Without Docker the tests are Inconclusive locally, but fail in CI (CI=true) so coverage can't silently vanish.
    /// </summary>
    public static class SqlServerContainer
    {
        public const string Category = "SqlServer";
        private const string Image = "mcr.microsoft.com/mssql/server:2022-latest";
        private const string Database = "Valorant";

        private static MsSqlContainer? _container;
        private static string? _connectionString;
        private static string? _startError;
        private static readonly object StartLock = new();

        /// <summary>Connection string to the test database inside the container (null until started).</summary>
        public static string? ConnectionString => _connectionString;

        /// <summary>
        /// Starts the container (first call only), then turns SQL Server on for the caller. Dispose the result to turn it
        /// back off. Marks the test Inconclusive (or Failed in CI) when Docker isn't available.
        /// </summary>
        public static IDisposable Enable()
        {
            EnsureStarted();
            string connectionString = _connectionString!;

            AssertLocal(connectionString);
            DbSettings.SqlServer = connectionString;
            DbSettings.SqlServerEnabled = true;
            return new Scope();
        }

        /// <summary>Removes all rows from every table, so each test starts from an empty database.</summary>
        public static void ClearAllTables()
        {
            EnsureStarted();
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(
                // Foreign keys between the heatmap tables: disable them, clear every table, re-enable and re-validate.
                "EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'; " +
                "EXEC sp_MSforeachtable 'DELETE FROM ?'; " +
                "EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';", connection);
            command.ExecuteNonQuery();
        }

        /// <summary>Runs a scalar query against the test database (for assertions).</summary>
        public static object? Scalar(string sql, params (string Name, object Value)[] parameters)
        {
            EnsureStarted();
            using var connection = new SqlConnection(_connectionString);
            connection.Open();
            using var command = new SqlCommand(sql, connection);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
            object? result = command.ExecuteScalar();
            return result is DBNull ? null : result;
        }

        public static async Task StopAsync()
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
                _container = null;
            }
        }

        private static void EnsureStarted()
        {
            lock (StartLock)
            {
                if (_connectionString is null && _startError is null)
                {
                    try
                    {
                        Start();
                    }
                    catch (Exception ex)
                    {
                        _startError = ex.GetType().Name + ": " + ex.Message;
                    }
                }
            }

            if (_startError is not null)
            {
                string message = $"SQL Server container unavailable (is Docker running?): {_startError}";
                if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail(message);
                }
                Assert.Inconclusive(message);
            }
        }

        private static void Start()
        {
            var container = new MsSqlBuilder().WithImage(Image).Build();
            container.StartAsync().GetAwaiter().GetResult();
            _container = container;

            string masterConnection = container.GetConnectionString();
            AssertLocal(masterConnection);

            string schemaDir = Path.Combine(AppContext.BaseDirectory, "SqlServerSchema");
            foreach (string script in Directory.GetFiles(schemaDir, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
            {
                ApplyScript(masterConnection, File.ReadAllText(script));
            }

            _connectionString = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = Database }.ConnectionString;
        }

        /// <summary>Runs a script that uses GO batch separators (as SSMS does) on one connection, so USE carries over.</summary>
        private static void ApplyScript(string connectionString, string script)
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            foreach (string batch in Regex.Split(script, @"^\s*GO\s*;?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                using var command = new SqlCommand(batch, connection);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>Fails the test unless the connection string points at this machine (the container's mapped port).</summary>
        public static void AssertLocal(string connectionString)
        {
            string dataSource = new SqlConnectionStringBuilder(connectionString).DataSource;
            string host = dataSource.Split(',')[0].Trim();
            if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase)) host = host[4..];

            bool isLocal = host is "127.0.0.1" or "localhost" or "::1" or "[::1]" or "host.docker.internal";
            Assert.IsTrue(isLocal, $"Refusing to enable SQL Server for a non-local server '{host}'. Tests may only use the Testcontainers instance.");
        }

        private sealed class Scope : IDisposable
        {
            public void Dispose()
            {
                DbSettings.SqlServerEnabled = false;
                DbSettings.SqlServer = null!;
            }
        }
    }
}
