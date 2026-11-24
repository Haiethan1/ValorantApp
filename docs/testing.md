# Testing & validation

How to prove a change works in this repo. Design and rationale: [plans/e2e-validation.md](plans/e2e-validation.md).

## Commands
```bash
dotnet build
dotnet test        # all tests: pure, local, no secrets, no network, no Discord, no SQL Server
```
`dotnet test` must be green before you call anything done. One test is `[Ignore]`d on purpose
(`MatchTest_Premier_Success`, see [backlog](backlog.md)).

## Safety: tests can never touch prod
`ValorantAppTests/TestSetup.cs` runs once per test run and:
- sets `DbSettings.SqlServerEnabled = false`, so every SQL Server write is skipped or a no-op, and
- points `DbSettings.Sqlite` at a throwaway temp file with the schema created.

Never enable SQL Server in a test and never read `App.config` from one. `DbSettings` (in
`ValorantApp/Database/Extensions/DbSettings.cs`) defaults to the old behavior (config values, SQL Server on), so
production is unchanged.

## What exists
| Layer | Files in `ValorantAppTests/` | Notes |
|---|---|---|
| Unit | `EpisodeActTests`, `EnumMappingTests` | Pure logic. Pin *current* behavior, including known-stale data. |
| Persisted-enum guard | `PersistedEnumTests` | Pins integer values of `Agents`, `EventType`, `Team`. Appending is allowed; reorder/delete/insert fails. Add new `Agents` members here when adding content. |
| Characterization | `MatchCreateFromJsonTests` | Golden values for `MatchesExtension`/`MatchStatsExtension.CreateFromJson`. Some pin suspected bugs (marked `NOTE: possible bug`, tracked in issue #140). When you fix one, update the pinned value. |
| API client | `HenrikApiTests` | Mocked HTTP + JSON fixtures in `MockJsonResponse/`. Old and due for a refactor. |
| Component | `DbSettingsTests`, `DevToolsTests`, `BaseValorantProgramTests` | Real DAOs against the temp SQLite DB, `RecordingNotifier` for Discord. |
| End-to-end | `PollTickE2ETests` | Real engine through one poll tick: seed user + channel, stub the `HenrikApiClient` HTTP client with a date-rewritten fixture, `RunPollTickAsync()`, assert DB rows and the recorded recap embed. Covers: new match stored and posted once, same match polled twice posts once, two tracked players in one match post one combined recap. |

Shared helpers: `RecordingNotifier` (fake `IDiscordNotifier`: records sent embeds and statuses), `TestSetup`.

## Seams (how production code is made testable)
- `DbSettings` (`Sqlite`, `SqlServer`, `SqlServerEnabled`): swap/disable databases.
- `IDiscordNotifier` / `DiscordNotifier`: all outbound Discord posts and the bot status go through it.
- `BaseValorantProgram.Start()` starts the timers (called from `RunBotAsync`); the constructor does not.
  `internal RunPollTickAsync()` and `RunDailyCheckAsync()` run one tick/daily job on demand
  (`InternalsVisibleTo ValorantAppTests`). Exceptions propagate, unlike the timer wrappers.
- `DevToolsExtension.DeleteMatchEverywhere(matchId)` / `GetLatestMatchIdForUser(puuid)`: "forget" a match so the
  poller re-processes it.

## Writing tests
- **Static state:** DAOs are static and the temp DB is shared across the run, so tests must not run in parallel
  (MSTest is sequential by default; don't add `[Parallelize]`) and must **clean up what they insert**
  (use `try/finally`).
- **Seed before constructing the program.** `BaseValorantProgram` loads users in its constructor and caches channel
  ids on first read.
- **Fixtures are old.** The poller drops matches older than 3 days, so rewrite a fixture's start time to "now"
  before serving it. Prefer the non-competitive Swiftplay fixture (`Matches_10_31_2023.json`) for poll-tick tests:
  competitive matches also need an mmr-history response (no fixture yet) and trigger SQL-Server-only heatmap code.
- **Time:** code reads `DateTime.UtcNow` directly (daily report, tier change, 3-day cutoff). Daily-report tests
  stay date-fragile until `TimeProvider` is added (planned).
- **E2E recipe** (copy `PollTickE2ETests`): seed via DAOs, build the program with `RecordingNotifier` and a
  `Mock<IHttpClientFactory>` returning an `HttpClient` over a stub handler (route by path; unknown endpoints return
  404, which `HenrikApi` tolerates), call `RunPollTickAsync()`, assert, then clean up in `finally`
  (`DevToolsExtension.DeleteMatchEverywhere` + remove the seeded user/channel).
- A "does not happen" assertion (e.g. no duplicate post) must also prove the action was attempted (e.g. the second
  tick really polled), or it passes trivially. Mutation-check new tests: break the production behavior in a
  throwaway worktree and confirm the test fails.
- Don't "fix" a characterization test to what you think is right; fix the code (and the issue), then update it.

## Manual validation on the dev bot (until more is automated)
Run with a **debugger attached** (dev token + dev DBs). Never run the app without one from a dev machine: that
selects the prod token.
1. Start the bot. Expect `ShameBotDevelop is connected!` and `Registered global commands: ...` in `logs.txt`
   (next to the binary), then `Starting Send of scheduled messages.` one minute later and every ~30 s.
2. `/devdeletematch` (bot owner only, dev only) forgets your latest stored match in SQLite and SQL Server; the
   poller re-posts it on the next tick if it is still your latest match and under 3 days old.
3. To check the SQL Server dual write, compare row counts/latest `match_id` between SQLite `Matches`/`MatchStats` and
   SQL Server `lu_matches`/`lu_match_stats`.

## Not covered yet
- Slash commands (tightly coupled to Discord.Net socket types; needs logic extraction first).
- Daily report and tier-change flows (need a clock seam), competitive-match flow (needs an mmr-history fixture).
- Real-Discord smoke test, CI workflow, fixture refresh process.
