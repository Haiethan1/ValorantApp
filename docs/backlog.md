# Backlog: known issues & tech debt

Found during the Nov 2026 repo audit. Feature requests live in GitHub issues (`gh issue list`).
This file covers code-level debt. When an item gets an issue, link it and remove it here.

## Next up (planned)
- [ ] **Secrets & config redesign:** move secrets out of `App.config` into user-secrets (dev) and a
  prod secrets file outside the deploy dir, and replace `Debugger.IsAttached` with `VALORANTAPP_ENV`.
  Full plan: [plans/secrets-and-config.md](plans/secrets-and-config.md). Issue: #141.
- [ ] **Validation skill** (`.claude/skills/validate`): a repeatable way for agents to verify changes
  beyond `dotnet build`/`dotnet test`. To design:
  - Tiers: build + unit tests → tests against a copy of the prod SQLite DB → dev bot run in the dev
    Discord server (debugger / `Development` env).
  - Seeding a local dev DB (owner's test user + channel mapping) without touching prod.
  - How an agent can safely exercise slash commands and recap embeds (possibly a "dry-run" mode that
    renders embeds to console/files instead of posting).
  - More HenrikDev JSON fixtures in `ValorantAppTests/MockJsonResponse/` for new modes and content.
  - What the owner must check by hand, and how agents ask for it.

## Bugs / broken
- **Quarantined test:** `MatchTest_Premier_Success` (`ValorantAppTests/HenrikApiTests.cs`, marked `[Ignore]`)
  expects `mode=Premier`, but `HenrikApi.Match` lower-cases the mode (`mode=premier`). The existing tests were
  written ad hoc and are to be refactored under [plans/e2e-validation.md](plans/e2e-validation.md); decide
  which side is right then, and remove the `[Ignore]`.
- **Stale episode/act table** (`Valorant/Helpers/EpisodeActInfos.cs`): last entries `E25A2`/`E25A3`
  have `Act = 1`, overlapping ranges, and placeholder 2030 end dates. Riot's 2025+ naming (V25 acts)
  isn't modeled. A TODO in the file suggests fetching from the API instead.
- **Stale game content:** new agents, maps, and modes since the last update aren't in the enums.
  Unknown values fall back to `Unknown`. Use the `update-game-content` skill.

## Half-finished features
- **Heatmap** (`/heatmap`): owner-gated by a hardcoded Discord ID, returns a placeholder `result.png`.
  Event and location capture writes to SQL Server only.
- **SQL Server migration:** dual writes exist, reads never moved. Decide whether to finish it or go
  back to SQLite with batched/transactional writes. See [data-model.md](data-model.md).
- Embed pagination for reports with >20 items (TODO in `BaseValorantProgram.cs`).

## Tech debt
- Secrets in `App.config` and `Debugger.IsAttached` env selection: see "Next up".
- Sync-over-async (`.Result`, `.GetAwaiter().GetResult()`) in `HenrikApi.cs`, `SqlExtensions.cs`,
  `BaseValorantProgram.cs`, `Program.cs`.
- Rate limiting is passive (logs only). No backoff on 429.
- Dead code: `DiscordBot/DiscordBotCommand.cs`. Obsolete `MatchStats` columns (map/mode/rounds/length,
  moved to `Matches` in Mar 2024).
- SQLite connection string declares `providerName="System.Data.SqlClient"` (cosmetic, unused).
- CI runs build + tests (no deploy step). No Dockerfile, no automated backups (SQL Server has none at all).
- No process supervision on the Pi: the bot runs under `nohup` and stays down after a reboot or crash
  until someone starts it by hand. A systemd unit (`Restart=on-failure`, enabled at boot) would fix
  both, and would also replace the unbounded `nohup.out` with journald.
- Test coverage is ~52% lines (see docs/testing.md "Not covered yet"). Biggest gaps: SQL Server dual writes, slash command handlers other than linking. (API failures and routing are now covered by `ApiFailureE2ETests` and `RoutingE2ETests`.)
- **Daily/act report can be silently dropped** (issue #142): `SendReport` (`BaseValorantProgram.cs` ~1077) paginates by 20 fields only. With worst-case animated emojis a page reaches ~6100+ chars, Discord.Net throws (limit 6000), and the `catch` logs it and drops that channel's report. Pinned by `[Ignore]`d test in `EmbedLimitTests`; fix by also splitting on total embed length, then un-ignore.
- **Delete/unlink during a poll tick races with the tick:** `DeleteUser` -> `ReloadFromDB` calls `ReloadUsers()` (clears and rebuilds `Users`) *before* taking `QueueUsersLock`, so during that window the tick's `GetValorantUser` lookups can return null for users who were not deleted, and their recap is skipped for that tick. `ReloadQueue()` then blocks the slash-command thread until the tick finishes. Low impact (needs an /unlink during the few seconds of a tick). Not verified whether a skipped user's recap is picked up on a later tick. Fix: take the lock around the whole reload, or swap in a new dictionary instead of Clear+refill. `RoutingE2ETests.PollTick_UserDeletedMidTick_*` covers the post-rebuild interleaving.
- **One user's network failure skips the whole poll batch:** in `UpdateMatchQueueUsers`, `GetQueueUsersMatchStats(...).Result` throws an AggregateException if any of the 15 requests fails at the transport level (connection refused/reset, timeout), and the outer catch drops the whole batch for that tick. Users are re-queued, so it's a delay, not data loss. HTTP error statuses don't trigger it (those return null per user). Found by the replay harness (2026-11-24).
- **Stale game content in real 2026 data:** map `Summit` and agent `Hush` resolve to `Unknown`. Pinned by `[Ignore]`d tests in `RecordedFixtureContractTests`; fix with the `update-game-content` skill, then remove the `[Ignore]`s.
- **HenrikDev error envelope mismatch:** errors now come as `errors: [{code, message, status, details}]` (array, no top-level `status`), while `JsonObjectHenrik` expects a single object. Parsing fails safely (returns null), so the logged error message is lost. See `RecordedFixtureContractTests.AccountNotFound_*`.
- Rank tiers 0-2 (unranked) have no name mapping (`ToDescriptionString` returns the number). Pinned by an `[Ignore]`d test in `GameContentGuardTests`.
- Misc TODOs: `ITable.cs:37`, `MatchStatsExtension.cs:257,278,521`, `BaseValorantProgram.cs:291,378,804`,
  `RankEmojisEnum.cs:68`, `HenrikApiTests.cs:293`.
