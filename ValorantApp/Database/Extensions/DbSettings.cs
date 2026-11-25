using System.Configuration;
using System.Diagnostics;

namespace ValorantApp.Database.Extensions
{
    /// <summary>
    /// Connection settings for the DAO classes. Defaults come from App.config exactly as before
    /// (read lazily on first use), but can be overridden, e.g. by tests.
    /// </summary>
    public static class DbSettings
    {
        private static string? _sqlite;
        private static string? _sqlServer;

        /// <summary>SQLite connection string (source of truth for reads).</summary>
        public static string Sqlite
        {
            get => _sqlite ??= ConfigurationManager.ConnectionStrings["Database"].ConnectionString;
            set => _sqlite = value;
        }

        /// <summary>
        /// SQL Server connection string (dual writes + heatmap tables). Only readable when
        /// <see cref="SqlServerEnabled"/> is true; otherwise the getter throws (fail closed), so a DAO that forgets its
        /// SqlServerEnabled check can never connect to a real server from tests or test mode.
        /// </summary>
        public static string SqlServer
        {
            get
            {
                if (!SqlServerEnabled)
                {
                    throw new InvalidOperationException("SQL Server access is disabled (DbSettings.SqlServerEnabled = false); the caller must check SqlServerEnabled before using the connection string.");
                }

                return _sqlServer ??= ConfigurationManager.ConnectionStrings[Debugger.IsAttached ? "DatabaseDev" : "DatabaseProd"].ConnectionString;
            }
            set => _sqlServer = value;
        }

        /// <summary>
        /// When false, every SQL Server write is skipped (dual writes) or becomes a no-op (SQL Server only
        /// tables, which return false/0). Defaults to true to keep production behavior unchanged. Tests must
        /// set this to false so they can never reach a real server.
        /// </summary>
        public static bool SqlServerEnabled { get; set; } = true;
    }
}
