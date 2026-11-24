# Testing & validation

How to prove a change works in this repo. Design and rationale: [plans/e2e-validation.md](plans/e2e-validation.md).

## Commands
```bash
dotnet build
dotnet test        # all tests: pure, local, no secrets, no network, no Discord, no SQL Server
```
`dotnet test` must be green before you call anything done. Two tests are skipped on purpose: one is `[Ignore]`d
(`MatchTest_Premier_Success`, see [backlog](backlog.md)) and one is the opt-in real-Discord smoke test (below).

## CI
`.github/workflows/ci.yml` runs restore, build (Release) and test on Linux for every PR and for pushes to `main` and
`develop`. It uses no secrets and must stay that way: the suite is hermetic by design. If CI is red, reproduce locally
with the same three commands (`dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`). Linux-only
failures are most likely path case, or time zone ids (the code uses the Windows id `"Eastern Standard Time"`,
which needs ICU/tzdata on Linux).

## Coverage
Generate code coverage locally using the Coverlet collector:
```bash
dotnet test --collect:"XPlat Code Coverage"
```
The Cobertura report is output to `TestResults/<guid>/coverage.cobertura.xml`.

Notes:
- `OutOfProcessE2ETests` starts the application as a separate child process, so code executed out-of-process is not counted by the in-process coverage collector.
- In CI, the test step collects coverage and an inline script prints a per-file and overall line/branch summary to the GitHub Actions job summary (`$GITHUB_STEP_SUMMARY`). The Cobertura XML is also uploaded as a workflow artifact.

## Safety: tests can never touch prod
`ValorantAppTests/TestSetup.cs` runs once per test run and:
- sets `DbSettings.SqlServerEnabled = false`, so every SQL Server write is skipped or a no-op, and
- points `DbSettings.Sqlite` at a throwaway temp file with the schema created.

Never enable SQL Server in a test and never read `App.config` from one. `DbSettings` (in
`ValorantApp/Database/Extensions/DbSettings.cs`) defaults to the old behavior (config values, SQL Server on), so
production is unchanged.

**Never start the real executable without `VALORANTAPP_TESTMODE=1` from a test or script.** Without a debugger the app
selects the production bot token. See "Test mode" below.

## What exists
| Layer | Files in `ValorantAppTests/` | Notes |
|---|---|---|
| Unit | `EpisodeActTests`, `EnumMappingTests`, `TimerUtilsTests`, `AppStartupTests` (option parsing) | Pure logic. Pin *current* behavior, including known-stale data. |
| Persisted-enum guard | `PersistedEnumTests` | Pins integer values of `Agents`, `EventType`, `Team`. Appending is allowed; reorder/delete/insert fails. Add new `Agents` members here when adding content. |
| Characterization | `MatchCreateFromJsonTests` | Golden values for `MatchesExtension`/`MatchStatsExtension.CreateFromJson`. Some pin suspected bugs (marked `NOTE: possible bug`, tracked in issue #140). When you fix one, update the pinned value. |
| API client | `HenrikApiTests` | Mocked HTTP + JSON fixtures in `MockJsonResponse/`. Old and due for a refactor. |
| Component | `DbSettingsTests`, `DevToolsTests`, `BaseValorantProgramTests`, `LinkCommandServiceTests`, `AppStartupTests` (DI composition) | Real DAOs against the temp SQLite DB, `RecordingNotifier` for Discord. `LinkCommandServiceTests` pins every message of `/addme`, `/addchannel`, `/deletechannel`, `/deleteuser`. |
| End-to-end (in-process) | `PollTickE2ETests`, `DailyReportE2ETests` | Real engine, stubbed `HenrikApiClient` HTTP client, temp SQLite, `RecordingNotifier`, and an injected clock. Poll tick: new match stored and posted once, same match polled twice posts once, two tracked players post one combined recap, the 3-day cutoff on both sides. Daily job: the exact report window boundaries and the act-end report. |
| End-to-end (out-of-process) | `OutOfProcessE2ETests` | Starts the real `ValorantApp.dll` as a child process in test mode, with a fake HenrikDev server on loopback (`HttpListener`) and a seeded temp SQLite file. Asserts what lands in the recorder file and the DB, that a re-polled match is not posted twice, and that test mode refuses a non-loopback URL. This is the "build it, run it, prove it works" check for agents. The helper always sets `VALORANTAPP_TESTMODE=1`; never add a test that launches the executable without it. |
| Snapshot | `EmbedSnapshotTests` | Approval-style snapshot tests of full Discord embeds (single/combined non-comp recaps, competitive recaps, tier changes, daily and act reports). |
| Real Discord (local only) | `RealDiscordSmokeTests` | Opt-in, see below. Skipped by default and in CI. |

Shared helpers: `RecordingNotifier` (fake `IDiscordNotifier`: records sent embeds and statuses), `FixedTimeProvider`
(test clock), `TestSetup`.

## Seams (how production code is made testable)
- `DbSettings` (`Sqlite`, `SqlServer`, `SqlServerEnabled`): swap/disable databases.
- `IDiscordNotifier` / `DiscordNotifier`: all outbound Discord posts and the bot status go through it.
- `BaseValorantProgram.Start()` starts the timers (called from `RunBotAsync`); the constructor does not.
  `internal RunPollTickAsync()` and `RunDailyCheckAsync()` run one tick/daily job on demand
  (`InternalsVisibleTo ValorantAppTests`). Exceptions propagate, unlike the timer wrappers.
- `TimeProvider` (optional constructor argument, system clock by default): the 3-day cutoff, the daily report window,
  the act-end check, the tier-change season lookup and the daily timer start all read it. Slash-command time reads
  (`DiscordBotSlashCommands`) and `EpisodeActInfos.cs:83` still use the system clock.
- `LinkCommandService`: the logic of the linking slash commands, with plain ids/names in and a `CommandResult` out.
  The slash handlers only translate the Discord interaction and show the result.
- `AppOptions` + `ValorantApp.BuildServices(options)`: startup options and a testable service container (it constructs
  the Discord client but never connects).
- `DevToolsExtension.DeleteMatchEverywhere(matchId)` / `GetLatestMatchIdForUser(puuid)`: "forget" a match so the
  poller re-processes it.

## Test mode (running the real executable without Discord)
Opt-in with `VALORANTAPP_TESTMODE=1`; fails closed (`ValorantApp/AppOptions.cs`). It requires:
- `VALORANTAPP_SQLITE_PATH`: the SQLite file to use (never falls back to the configured database),
- `VALORANTAPP_HENRIK_URL`: must be a **loopback** http(s) URL (a fake server),
- `VALORANTAPP_RECORD_PATH`: a JSON-lines file where `JsonLinesNotifier` appends every embed and status,
- optional `VALORANTAPP_POLL_INITIAL_DELAY_MS` / `VALORANTAPP_POLL_INTERVAL_MS` (default 200 ms / 500 ms).

In test mode the app never reads bot tokens or `HenrikToken`, never logs into Discord, and disables SQL Server writes.
It prints `READY` once the timers run, then blocks until stdin is closed. Outside test mode these variables must not
be set: startup throws if they are, so they can never look like production overrides.

## Real Discord smoke test (local only)
`RealDiscordSmokeTests` posts one embed through the real `DiscordNotifier`, reads it back, deletes it, and sets the
status. It checks what fakes cannot (the real client against real Discord). It is skipped unless you set:
- `VALORANTAPP_SMOKE_BOT_TOKEN`: the **dev** bot token, and
- `VALORANTAPP_SMOKE_CHANNEL_ID`: a channel in your dev server the bot can post in.

It refuses to run as a bot whose name does not contain "develop" (override with `VALORANTAPP_SMOKE_ALLOW_ANY_BOT=1`
only if you know why). Run just it with `dotnet test --filter TestCategory=RealDiscord`. Slash commands cannot be
triggered by a bot, so their logic is tested through `LinkCommandService` instead.

## Snapshot tests
`ValorantAppTests/EmbedSnapshotTests.cs` runs approval-style tests of complete Discord embeds across all primary bot notifications (non-competitive single recap, two-player combined recap, competitive recap, competitive promotion and demotion tier-change embeds, daily report summary, and act-end report).
- Embed snapshots are serialized to indented JSON capturing all visible fields: `title`, `description`, `url`, `color`, `thumbnailUrl`, `imageUrl`, `author`, `footer`, `timestampPresence`, and `fields` (name/value/inline).
- Approved baselines live in `ValorantAppTests/Snapshots/*.approved.json` and copy to the build output directory via `ValorantAppTests.csproj`.
- If an approved file is missing or differs from test output, `<name>.received.json` is written next to it and the test fails with a clear message.
- To generate or update approved snapshots, set `VALORANTAPP_UPDATE_SNAPSHOTS=1` and run the tests:
  ```bash
  VALORANTAPP_UPDATE_SNAPSHOTS=1 dotnet test --filter EmbedSnapshotTests
  ```
- All snapshots are deterministic: they use injected `FixedTimeProvider` instances and fake fixtures with no wall-clock dependencies or randomized identifiers.

## Writing tests
- **Static state:** DAOs are static and the temp DB is shared across the run, so tests must not run in parallel
  (MSTest is sequential by default; don't add `[Parallelize]`; E2E classes are `[DoNotParallelize]`) and must **clean up
  what they insert** (use `try/finally`).
- **Seed before constructing the program.** `BaseValorantProgram` loads users in its constructor and caches channel
  ids on first read.
- **Fixtures are old.** The poller drops matches older than 3 days, so rewrite a fixture's start time before serving
  it (or fix the clock). Prefer the non-competitive Swiftplay fixture (`Matches_10_31_2023.json`) for poll-tick tests:
  competitive matches also need an mmr-history response (no fixture yet) and trigger SQL-Server-only heatmap code. The
  daily report only counts **competitive** matches, so its tests use the competitive fixture seeded straight into SQLite.
- **Time:** inject a `FixedTimeProvider` instead of relying on the real date.
- **E2E recipe** (copy `PollTickE2ETests`): seed via DAOs, build the program with `RecordingNotifier` and a
  `Mock<IHttpClientFactory>` returning an `HttpClient` over a stub handler (route by path; unknown endpoints return
  404, which `HenrikApi` tolerates), call `RunPollTickAsync()`, assert, then clean up in `finally`
  (`DevToolsExtension.DeleteMatchEverywhere` + remove the seeded user/channel).
- A "does not happen" assertion (e.g. no duplicate post) must also prove the action was attempted (e.g. the second
  tick really polled), or it passes trivially. Mutation-check new tests: break the production behavior in a
  throwaway worktree and confirm the test fails.
- Don't "fix" a characterization test to what you think is right; fix the code (and the issue), then update it.

## Manual validation on the dev bot (until more is automated)
Run with a **debugger attached** (dev token + dev DBs). Never run the app without one from a dev machine except in
test mode: that selects the prod token.
1. Start the bot. Expect `ShameBotDevelop is connected!` and `Registered global commands: ...` in `logs.txt`
   (next to the binary), then `Starting Send of scheduled messages.` one minute later and every ~30 s.
2. `/devdeletematch` (bot owner only, dev only) forgets your latest stored match in SQLite and SQL Server; the
   poller re-posts it on the next tick if it is still your latest match and under 3 days old.
3. To check the SQL Server dual write, compare row counts/latest `match_id` between SQLite `Matches`/`MatchStats` and
   SQL Server `lu_matches`/`lu_match_stats`.

## Not covered yet
- `/mmr` and `/heatmap` logic (still inside `DiscordBotSlashCommands`; `/heatmap` is unfinished).
- Tier-change flow and the competitive poll-tick flow (need an mmr-history fixture).
- SQL Server dual writes (tests disable them by design; verify manually on the dev bot).
- Fixture refresh process when the HenrikDev JSON shape changes.
