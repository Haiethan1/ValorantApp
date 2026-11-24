# Validation trust plan

Goal: the owner can trust AI-written changes without manually checking behavior, because a fast, hermetic
validation layer covers the bot's real scenarios. Background and design: [e2e-validation.md](e2e-validation.md).
How the tests work: [../testing.md](../testing.md). Known bugs and gaps: [../backlog.md](../backlog.md).

## Where we are
Branch `aiReady`. 204 passing tests, 4 skipped on purpose. Line coverage was ~52% (branch ~42%) before the
latest additions; re-measure with `dotnet test --collect:"XPlat Code Coverage"` (CI now prints a summary).

Built so far:
- Docs and project skills so an agent knows the rules (`AGENTS.md`, `docs/`, `.claude/skills/`).
- Seams: `DbSettings`, `IDiscordNotifier`, on-demand poll/daily runs, `TimeProvider`, `AppOptions` with fail-closed
  test mode, `LinkCommandService`.
- Tests: unit, characterization, in-process E2E (poll tick, competitive and tier change, daily and act report),
  out-of-process E2E (real exe in test mode), game-content guards, embed size limits, embed snapshots, API failure matrix, channel routing and user rotation.
- CI: build + test + coverage summary, no secrets.
- Safety: tests can never reach SQL Server or prod. `DbSettings.SqlServer` now throws when SQL Server is disabled.

## Plan items and status
Owner column: who should do it. Claude = security-sensitive or needs design judgment. Gemini = well-specified, hand to
Antigravity, Claude reviews.

| # | Item | Size | Owner | Status |
|---|---|---|---|---|
| 1 | Fail-closed SQL Server setting | S | Claude | Done |
| 1b | Stop copying `ValorantApp.dll.config` into the test build output | S | Claude | Done (guard test in `DbSettingsTests`) |
| 2 | Game-content guards (every agent/map/mode/rank has emoji, image, round trip) + unknown-content E2E | S | Gemini | Done |
| 3 | Embed snapshot tests (full embed output, approved files) | M | Gemini | Done |
| 4 | Competitive poll + tier-change E2E (promotion, demotion, none) | M | Gemini | Done (double-up, first ranked match, empty history not covered) |
| 5 | API failure matrix (404/429/5xx, timeout, bad JSON, empty data; no post, user re-queued, next tick recovers) | M | Gemini drafts, Claude reviews timeouts | Done (`ApiFailureE2ETests`, no bugs found) |
| 6 | Routing: multiple channels, split channels, 16+ users rotating over ticks, user deleted mid-tick | M | Gemini | Done (`RoutingE2ETests`; found a delete-mid-tick race, see backlog) |
| 7 | Embed-limit tests (Discord 6000 total, 25 fields, etc.) | S | Gemini | Done, found a bug (issue #142) |
| 8 | Opt-in fixture recorder (dev HenrikDev key, strip personal data) + contract test over all fixtures; refresh monthly | M | Claude designs | Not started. Needs the owner's API key and live matches |
| 9 | Old-vs-new replay: run base and PR commits in test mode on the same fixtures and a copy of the prod DB, diff the recorder output | M | Claude | Not started. Strongest "trust the change" signal |
| 10 | SQL Server in CI (Testcontainers `mssql/server:2022`) to test dual writes, events and locations | L | Claude sets up, Gemini writes asserts | Not started. Docker isn't running locally; CI (ubuntu) has it |
| 11 | Mutation testing (Stryker.NET) on engine, `LinkCommandService`, `MatchesExtension`, `MatchStatsExtension`; per release | M | Gemini runs, Claude triages | Not started |
| 12 | Move `/mmr` logic into a service like `LinkCommandService` and test it; fix review nits and stale docs | S-M | Gemini | Done (`MmrCommandService` + tests; null `Current_Data` now replies instead of throwing) |

Review nits: all fixed except `DateTime.TryParse` using machine culture (pre-existing production behavior, left as is).

## Decisions (2026-11-24)
1. Report overflow bug: filed as issue #142, fixed separately. Low likelihood at the current scale.
2. The accidental "x" commit was reworded to `aiReady: Add Discord embed size-limit tests` (tree unchanged; old
   history kept at tag `backup/aiReady-before-reword` until the branch is pushed).
3. Items 5, 6 and 12 done via Antigravity, reviewed and fixed up by Claude.
4. Secrets redesign stays in issue #141.

## Open questions for the owner
- Item 8 (fixture recorder): needs the owner's HenrikDev key and a decision on scope.
- Item 10: run SQL Server tests only in CI, or also locally with Docker Desktop?
- Pi deployment docs (`docs/hosting.md`, `deploy-pi` skill) need the owner's review.

## What will honestly still need a human
- Custom emoji IDs, embed look, and slash command registration: owner confirmed OK on 2026-11-24. Re-check after
  changes to those areas.
- Pi deployment (arm64 build, process supervision).
- Riot season dates in `EpisodeActInfos` (known stale and not working as intended; see backlog).
- Deciding what a HenrikDev API change means once the contract test (item 8) flags it.
