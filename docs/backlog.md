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
- Test coverage is ~52% lines (see docs/testing.md "Not covered yet"). Biggest gaps: SQL Server dual writes, API failure handling, slash command handlers other than linking.
- **Daily/act report can be silently dropped:** `SendReport` (`BaseValorantProgram.cs` ~1077) paginates by 20 fields only. With worst-case animated emojis a page reaches ~6100+ chars, Discord.Net throws (limit 6000), and the `catch` logs it and drops that channel's report. Pinned by `[Ignore]`d test in `EmbedLimitTests`; fix by also splitting on total embed length, then un-ignore.
- Rank tiers 0-2 (unranked) have no name mapping (`ToDescriptionString` returns the number). Pinned by an `[Ignore]`d test in `GameContentGuardTests`.
- Misc TODOs: `ITable.cs:37`, `MatchStatsExtension.cs:257,278,521`, `BaseValorantProgram.cs:291,378,804`,
  `RankEmojisEnum.cs:68`, `HenrikApiTests.cs:293`.
