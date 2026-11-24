# Plan: validation layer (unit, component, end-to-end)

**Status:** implemented on the `aiReady` branch (Nov 2026), after an independent code review of the first draft.
What exists today and how to use it: [../testing.md](../testing.md). Remaining items are listed under "Open questions".

## Goal
The repo should let an AI agent (or a human) build the app, run it, and *prove* a change works, so
agent output can be trusted without manual poking. Part of a wider "AI software factory" idea: each repo
documents how to code, test and validate in it. This plan is the testing/validation part for this repo.

## Where we are
- Tests: only `ValorantAppTests/HenrikApiTests.cs` (mocked HTTP + JSON fixtures). There are only 4 tests,
  written ad hoc and due for a refactor. `MatchTest_Premier_Success` is quarantined with `[Ignore]` (done),
  so `dotnet test` is green, but "tests pass" is weak proof until the suite is rebuilt.
- The app depends on four external things, none of which tests can currently replace:
  - **Discord**: `DiscordSocketClient` used directly.
  - **HenrikDev**: named `HttpClient` `"HenrikApiClient"` (`HenrikApi.cs:48`). A test can already register
    that name with a stub handler (see `HenrikApiTests.cs:37-44`), so the hardcoded base URL in
    `Program.cs` is *not* a blocker for in-process tests.
  - **SQLite**: path from config, but DAOs are static and read `static readonly` connection strings
    (`BaseTable.cs:8-9`), set once from `ConfigurationManager`.
  - **SQL Server (NAS)**: writes happen inline right after SQLite writes with no try/catch
    (`MatchesExtension.cs:73-102`). Four DAOs are SQL Server only (`MatchEvents`, `MatchPlayers`,
    `ValorantPlayers`, `PlayerLocations`). `BaseTable.cs:9` selects `DatabaseProd` whenever no debugger
    is attached.
- Time: read statically via `DateTime.UtcNow` (3-day match cutoff at `BaseValorantProgram.cs:322-323`, daily
  report, tier change, `TimerUtils`). All JSON fixtures are from 2023/24, so they'd be skipped as too old.
- `BaseValorantProgram`'s constructor has side effects: it loads users from the DB and starts both timers
  (`:30-33`). The poll timer fires after 1 minute (`async void`, `:202`) and would race a manual tick.
  The poll body (`SendScheduledMessage`, `:219`) is private.
- `Program.Main` does config, DB init, DI, and Discord login in one method.

## Layers
| Layer | Proves | Needs |
|---|---|---|
| Unit | `MatchStatsExtension.CreateFromJson` / `MatchesExtension.CreateFromJson` (public, static, no DB), enums, `EpisodeActInfos`, embed formatting, rank promotion/demotion | little |
| Component | DAOs against a temp SQLite file | settable DB settings |
| E2E, deterministic | Real app code with a stubbed Henrik handler, temp SQLite, recording notifier: a new match is stored and a recap embed is "sent" to the right channel | seams below |
| E2E, real smoke | Bot logs into the **dev** Discord server and a slash command gets a real response | dev token; local only, not CI/cloud |

The deterministic E2E is the main target: no secrets, repeatable, runnable by cloud agents and CI.

## Safety rule (fail closed)
Tests and test mode must never touch prod. The SQL Server switch defaults to **off** in the test
environment, and test mode must never call `LoginAsync` or use `BotToken`. A plain `dotnet test` on a
machine with real config must not be able to write to the prod NAS.

## Discord abstraction
Discord is used two ways.
1. **Outbound** (poller and daily report). All posts go through `DiscordExtensions.CheckChannelAndSendMessageAsync`
   (4 call sites: `BaseValorantProgram.cs:717/752/874/1049`) plus `SetGameAsync` (`:1080`) and a guild count
   read (`:1077`, also reached from `Program.ReadyAsync`). Abstract this:
   ```csharp
   interface IDiscordNotifier {
       Task SendEmbedAsync(ulong channelId, string? text, EmbedBuilder embed);
       Task SetStatusAsync(string text);
       int GuildCount { get; }
   }
   ```
   Production `DiscordNotifier` wraps the real client and keeps today's behavior: `embed.Build()`, the 250 ms
   delay (`DiscordExtensions.cs:13`), the "Could not find channel" warning. Tests use a `RecordingNotifier`
   that stores the built `Embed`.
2. **Inbound slash commands** (`DiscordBotSlashCommands`) use Discord.Net socket types directly and can't be
   practically faked. Out of scope here. Later: move command logic into plain services with thin handlers.
   Until then they are covered only by the real-Discord smoke tier.

## Seams (separate small PRs, in this order)
1. **DB settings + SQL Server switch. DONE, verified on the dev bot (dual writes still land).** `DbSettings` (lazy defaults from
   App.config, settable) + 13 guarded SQL Server sites; the test assembly sets `SqlServerEnabled = false` and a temp
   SQLite file in `TestSetup.cs` (fail closed). Original plan: Replace the two `static readonly` fields in `BaseTable` with a
   settable `DbSettings { Sqlite; SqlServer?; SqlServerEnabled }`. `Main` fills it from config. Wrap each
   SQL Server block (about 10 sites) in `if (SqlServerEnabled)`; SQL-Server-only DAOs no-op or return 0
   (`EventTotalCount` runs in `UpdateDiscordBotConfig`). DAOs stay static. `ITable.CreateTables(string)`
   already takes a connection string.
2. **`IDiscordNotifier`. DONE, verified on the dev bot (recap re-posted via the notifier).** `IDiscordNotifier`, `DiscordNotifier` (wraps the
   existing `CheckChannelAndSendMessageAsync`, behavior unchanged), registered in `Program.cs`; `RecordingNotifier`
   lives in the test project. Original plan: as above, replacing the 4 send sites and the `:1077/:1080` calls.
3. **Poll tick + `Start()`. DONE, verified on the dev bot (first tick at +60 s, then every ~30 s).** `Start()` is called from `RunBotAsync`; `internal RunPollTickAsync()` and `RunDailyCheckAsync()` drive one tick on demand; `InternalsVisibleTo ValorantAppTests`. Original plan: Move timer start out of the constructor into `Start()` (called from
   `RunBotAsync`). Expose one tick as an `internal RunPollTickAsync()` with `InternalsVisibleTo` for the test
   project.

Added later, as planned:
4. **`TimeProvider`. DONE.** Optional `BaseValorantProgram` constructor argument (system clock by default); used by the
   3-day cutoff, daily report window, act-end check, tier-change season lookup and the daily timer start. Test clock:
   `FixedTimeProvider`.
5. **`Program.Main` split + `AppOptions` + test mode. DONE.** `AppOptions` (Henrik URL is now an option), testable
   `BuildServices`, fail-closed `VALORANTAPP_TESTMODE` with `JsonLinesNotifier` (see `docs/testing.md`).
6. **`LinkCommandService`. DONE.** The logic of `/addme`, `/addchannel`, `/deletechannel`, `/deleteuser` extracted from the
   slash handlers (messages and flow unchanged).

## First E2E test (thin slice)
Temp SQLite + `CreateTables`; seed one `ValorantUsers` row and one `ValorantChannelMappings` row *before*
resolving the program (`ChannelIds` is cached on first read, `BaseValorantUser.cs:83`); stub the named
`HttpClient` with the **Swiftplay** fixture (`Matches_10_31_2023`) with its start date rewritten to "now";
run one tick; assert a `MatchStats` row exists and exactly one recorded embed went to the seeded channel
with the expected title. Why Swiftplay: the competitive fixture needs an mmr-history response that has no
fixture (`matchHistories` is always empty, `:689`; the fallback at `:340` and rejection at `:390-396`), and
triggers SQL-only heatmap inserts (`:426`) including an un-awaited background `Task` (`:428`).
This test doubles as the characterization lock for the recap embed.

Static DB state means E2E tests cannot run in parallel; mark the collection non-parallel.

## In-process vs out-of-process
Start **in-process**: the test builds the service provider (named `HttpClient` stub, temp DB,
`RecordingNotifier`), triggers a tick, asserts on DB rows and recorded messages. Later, add a test-mode flag so
the same fakes run out of process: real executable, never logs into Discord, SQL Server hard-disabled,
recorder writes to a file or local endpoint. That is what an agent's "build and run it" check would use. It
needs the `Main` split and a configurable Henrik URL.

## Order
0. ~~Quarantine `MatchTest_Premier_Success`~~ (done, backlog updated). Add pure characterization tests for
   the two `CreateFromJson` methods.
1. Seams 1-3 as separate small PRs. With no safety net yet, run each once against the **dev** bot with a
   debugger attached.
2. ~~The thin E2E test above.~~ (done: `PollTickE2ETests`, mutation-checked.)
3. ~~`docs/testing.md` and an AGENTS.md "how to validate" section~~ (done; update when the E2E tests land).
4. ~~Add `TimeProvider`, then E2E tests for the daily report~~ (done: `DailyReportE2ETests`, mutation-checked).
   The tier-change flow is not covered: it needs a competitive fixture with an mmr-history response.
5. ~~`Program.Main` split, configurable Henrik URL, out-of-process test mode~~ (done; the out-of-process harness test is
   `OutOfProcessE2ETests` if present in `docs/testing.md`).
6. ~~Unit/component backfill (agent-drafted, human-reviewed), then the real-Discord smoke tier.~~ (done: unit tests,
   `LinkCommandServiceTests`, and the opt-in `RealDiscordSmokeTests`. A bot cannot trigger slash commands, so the smoke
   test covers the notifier only; command logic is covered through `LinkCommandService`.)
7. ~~CI: a GitHub Actions workflow running build + test (no secrets needed).~~ (workflow written, passes locally in
   Release; first Linux run happens when it is pushed.)

## Non-goals
- Replacing the blocking `.Result` calls (`SqlExtensions.cs`, `HenrikApi.cs`, `BaseValorantUser.cs`,
  `BaseValorantProgram.cs:290/310`). They're fine on the thread pool; changing them is churn with regression risk.
- Faking the Discord gateway.
- The [secrets redesign](secrets-and-config.md) (issue #141), deferred until this validation existed. It should be the
  first change validated by it.

## Open questions
- Fixture refresh: how do we notice and update fixtures when the HenrikDev JSON shape changes?
- Tier-change and competitive poll-tick flows need an mmr-history fixture.
- `/mmr` and `/heatmap` logic is still inside `DiscordBotSlashCommands`.
- Resolved: daily-report determinism (inject a clock), real-Discord smoke tier (kept, opt-in, notifier only).
