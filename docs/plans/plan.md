# Validation trust plan

Goal: the owner can trust AI-written changes without manually checking behavior, because a fast, hermetic
validation layer covers the bot's real scenarios. Background and design: [e2e-validation.md](e2e-validation.md).
How the tests work: [../testing.md](../testing.md). Known bugs and gaps: [../backlog.md](../backlog.md).

## Where we are
Branch `aiReady`. 168 passing tests, 4 skipped on purpose. Line coverage was ~52% (branch ~42%) before the
latest additions; re-measure with `dotnet test --collect:"XPlat Code Coverage"` (CI now prints a summary).

Built so far:
- Docs and project skills so an agent knows the rules (`AGENTS.md`, `docs/`, `.claude/skills/`).
- Seams: `DbSettings`, `IDiscordNotifier`, on-demand poll/daily runs, `TimeProvider`, `AppOptions` with fail-closed
  test mode, `LinkCommandService`.
- Tests: unit, characterization, in-process E2E (poll tick, competitive and tier change, daily and act report),
  out-of-process E2E (real exe in test mode), game-content guards, embed size limits, embed snapshots.
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
| 5 | API failure matrix (404/429/5xx, timeout, bad JSON, empty data; no post, user re-queued, next tick recovers) | M | Gemini drafts, Claude reviews timeouts | Not started |
| 6 | Routing: multiple channels, split channels, 16+ users rotating over ticks, user deleted mid-tick | M | Gemini | Not started |
| 7 | Embed-limit tests (Discord 6000 total, 25 fields, etc.) | S | Gemini | Done, found a bug (see decisions) |
| 8 | Opt-in fixture recorder (dev HenrikDev key, strip personal data) + contract test over all fixtures; refresh monthly | M | Claude designs | Not started. Needs the owner's API key and live matches |
| 9 | Old-vs-new replay: run base and PR commits in test mode on the same fixtures and a copy of the prod DB, diff the recorder output | M | Claude | Not started. Strongest "trust the change" signal |
| 10 | SQL Server in CI (Testcontainers `mssql/server:2022`) to test dual writes, events and locations | L | Claude sets up, Gemini writes asserts | Not started |
| 11 | Mutation testing (Stryker.NET) on engine, `LinkCommandService`, `MatchesExtension`, `MatchStatsExtension`; per release | M | Gemini runs, Claude triages | Not started |
| 12 | Move `/mmr` logic into a service like `LinkCommandService` and test it; fix review nits and stale docs | S-M | Gemini | Not started |

Review nits still open (from the Opus review): `Start_CanBeCalledTwice` asserts nothing; E2E tests seed users before
`try` (partial-seed leaks); `DateTime.TryParse` uses machine culture (pre-existing); `JsonLinesNotifier` recorded fields
(now fixed by snapshots); stale NOTE line numbers in test comments.

## Open decisions (waiting on the owner)
1. **Report overflow bug.** `SendReport` (`BaseValorantProgram.cs` ~1077) paginates by 20 fields only. With
   worst-case animated emojis a page reaches ~6100+ characters, Discord.Net throws, and that channel's report is
   silently dropped. Pinned by an `[Ignore]`d test in `EmbedLimitTests`. Fix on `aiReady` (small, un-ignores the test)
   or file a GitHub issue and fix separately?
2. **Commit `6678749` has the message "x"** and no attribution (accidental). Squash/reword it (rewrites a few
   unpushed local commits) or leave it?
3. **Next up:** start items 5 (API failures) and 6 (routing)? Plan is two Antigravity jobs in separate worktrees
   with `AGY_TIMEOUT=1800s`.
4. **Secrets redesign** is tracked separately in issue #141 and waits until this branch is in a decent state.

## What will honestly still need a human
- Custom emoji IDs existing in the Discord guild, and how embeds look (tone and layout). The opt-in smoke test could
  be extended to fetch the bot's emojis and HEAD-check image URLs.
- Slash command registration, propagation and permissions.
- Pi deployment (arm64 build, process supervision).
- Riot season dates in `EpisodeActInfos`.
- Deciding what a HenrikDev API change means once the contract test (item 8) flags it.
