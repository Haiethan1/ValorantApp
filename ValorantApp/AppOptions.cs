namespace ValorantApp
{
    /// <summary>
    /// Startup options. The defaults reproduce the previous hardcoded behavior, so production is unchanged.
    ///
    /// TEST MODE is for out-of-process end-to-end tests and agent "build and run it" checks. It is opt-in through
    /// environment variables and fails closed: it needs an explicit SQLite path, a loopback HenrikDev URL and a
    /// recorder file; it never reads bot tokens, never logs into Discord, and disables SQL Server writes.
    /// Outside test mode the test variables must not be set (they would silently look like overrides), so
    /// startup throws if they are.
    /// </summary>
    public sealed class AppOptions
    {
        public const string DefaultHenrikBaseUrl = "https://api.henrikdev.xyz/valorant/";

        public const string TestModeVar = "VALORANTAPP_TESTMODE";
        public const string HenrikUrlVar = "VALORANTAPP_HENRIK_URL";
        public const string SqlitePathVar = "VALORANTAPP_SQLITE_PATH";
        public const string RecordPathVar = "VALORANTAPP_RECORD_PATH";
        public const string PollInitialDelayMsVar = "VALORANTAPP_POLL_INITIAL_DELAY_MS";
        public const string PollIntervalMsVar = "VALORANTAPP_POLL_INTERVAL_MS";
        public const string NowVar = "VALORANTAPP_NOW";
        public const string ManualVar = "VALORANTAPP_MANUAL";

        private static readonly string[] TestOnlyVars = [HenrikUrlVar, SqlitePathVar, RecordPathVar, PollInitialDelayMsVar, PollIntervalMsVar, NowVar, ManualVar];

        public bool TestMode { get; init; }

        /// <summary>HenrikDev base address, always with a trailing slash.</summary>
        public string HenrikBaseUrl { get; init; } = DefaultHenrikBaseUrl;

        /// <summary>Test mode only: SQLite file to use instead of the configured database.</summary>
        public string? SqlitePath { get; init; }

        /// <summary>Test mode only: JSON-lines file the notifier appends recaps and statuses to.</summary>
        public string? RecordPath { get; init; }

        public TimeSpan PollInitialDelay { get; init; } = TimeSpan.FromMinutes(1);

        public TimeSpan PollInterval { get; init; } = TimeSpan.FromMinutes(0.5);

        /// <summary>Test mode only: a frozen "now" (ISO-8601 UTC) used by the program's clock, for deterministic replays.</summary>
        public DateTimeOffset? FixedNow { get; init; }

        /// <summary>
        /// Test mode only: don't start the timers. The process is driven by stdin commands instead
        /// ("poll", "daily", "quit"), each answered with "OK &lt;command&gt;" or "ERR &lt;message&gt;".
        /// </summary>
        public bool Manual { get; init; }

        /// <summary>Reads options from environment variables. <paramref name="getEnv"/> exists for tests.</summary>
        public static AppOptions FromEnvironment(Func<string, string?>? getEnv = null)
        {
            getEnv ??= Environment.GetEnvironmentVariable;

            string? Get(string name)
            {
                string? value = getEnv(name);
                return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            }

            string? testModeValue = Get(TestModeVar);
            bool testMode = testModeValue is "1" || string.Equals(testModeValue, "true", StringComparison.OrdinalIgnoreCase);

            if (!testMode)
            {
                string? stray = TestOnlyVars.FirstOrDefault(v => Get(v) != null);
                if (stray != null)
                {
                    throw new InvalidOperationException($"{stray} is set but {TestModeVar} is not 1. Refusing to start: these variables are only valid in test mode.");
                }

                return new AppOptions();
            }

            string sqlitePath = Get(SqlitePathVar) ?? throw new InvalidOperationException($"Test mode requires {SqlitePathVar} (it must never fall back to the configured database).");
            string recordPath = Get(RecordPathVar) ?? throw new InvalidOperationException($"Test mode requires {RecordPathVar}.");
            string henrikUrl = Get(HenrikUrlVar) ?? throw new InvalidOperationException($"Test mode requires {HenrikUrlVar} pointing at a loopback fake server.");

            if (!Uri.TryCreate(henrikUrl, UriKind.Absolute, out Uri? henrikUri)
                || (henrikUri.Scheme != Uri.UriSchemeHttp && henrikUri.Scheme != Uri.UriSchemeHttps)
                || !henrikUri.IsLoopback)
            {
                throw new InvalidOperationException($"{HenrikUrlVar} must be an http(s) URL on a loopback host (localhost / 127.0.0.1 / [::1]) in test mode.");
            }

            return new AppOptions
            {
                TestMode = true,
                HenrikBaseUrl = henrikUrl.EndsWith('/') ? henrikUrl : henrikUrl + "/",
                SqlitePath = sqlitePath,
                RecordPath = recordPath,
                PollInitialDelay = ParseMilliseconds(Get(PollInitialDelayMsVar), PollInitialDelayMsVar, allowZero: true) ?? TimeSpan.FromMilliseconds(200),
                PollInterval = ParseMilliseconds(Get(PollIntervalMsVar), PollIntervalMsVar, allowZero: false) ?? TimeSpan.FromMilliseconds(500),
                FixedNow = ParseNow(Get(NowVar)),
                Manual = Get(ManualVar) is "1" || string.Equals(Get(ManualVar), "true", StringComparison.OrdinalIgnoreCase),
            };
        }

        private static DateTimeOffset? ParseNow(string? value)
        {
            if (value == null)
            {
                return null;
            }

            if (!DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out DateTimeOffset now))
            {
                throw new InvalidOperationException($"{NowVar} must be an ISO-8601 date/time, e.g. 2025-12-29T10:30:00Z.");
            }

            return now;
        }

        private static TimeSpan? ParseMilliseconds(string? value, string name, bool allowZero)
        {
            if (value == null)
            {
                return null;
            }

            if (!int.TryParse(value, out int ms) || ms < 0 || (ms == 0 && !allowZero))
            {
                throw new InvalidOperationException($"{name} must be a {(allowZero ? "non-negative" : "positive")} integer number of milliseconds.");
            }

            return TimeSpan.FromMilliseconds(ms);
        }
    }
}
