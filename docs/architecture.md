# Architecture

## Components
```
            ┌──────────────── Raspberry Pi 4 (prod) ────────────────┐
 Discord ◄──┤ ValorantApp (.NET 8, self-contained linux-arm64)      │
 gateway    │   ├─ Discord.Net client + slash commands              │
            │   ├─ BaseValorantProgram: 30s poll timer, daily timer │──► HenrikDev API
            │   ├─ SQLite myDatabase.db  (source of truth for reads)│    api.henrikdev.xyz/valorant/
            │   └─ logs.txt / apilogs.txt (Serilog, rolling 10 MB)  │
            └───────────────────────┬───────────────────────────────┘
                                    │ dual writes + heatmap data
                       ┌────────────▼────────────┐
                       │ UGREEN NAS (UGOS)       │
                       │ SQL Server in Docker    │
                       │  dev + prod containers  │
                       └─────────────────────────┘
```

## Startup (`ValorantApp/Program.cs`)
1. `ITable.CreateTables` runs `CREATE TABLE IF NOT EXISTS` against **SQLite only**. A missing
   `myDatabase.db` is created empty.
2. Serilog: two rolling file sinks in the working directory. `logs.txt` gets general logs.
   `apilogs.txt` gets entries tagged with the `ApiLog` property. 10 MB per file, 5 retained.
3. DI container: named `HttpClient` for HenrikDev (base `https://api.henrikdev.xyz/valorant/`,
   `Authorization: <HenrikToken>`, minimal HTTP logger), singletons for `BaseValorantProgram`,
   `DiscordSocketClient` (all gateway intents), `InteractionService`, `DiscordBotSlashCommands`.
4. Login with `BotToken` or `DevelopBotToken` (chosen by `Debugger.IsAttached`). On `Ready`, slash
   commands are bulk-overwritten globally.

## Runtime loops (`Valorant/BaseValorantProgram.cs`)
- **Match poll timer.** Starts after 1 min, then runs every 30 s. Dequeues up to 15 users from a
  round-robin queue, fetches each user's latest match from HenrikDev, skips already-stored matches,
  writes `Matches`/`MatchStats` (and SQL Server copies, plus heatmap event data), then posts a
  recap embed to every channel mapped to that player. Also posts rank promotions and demotions.
- **Daily timer.** Fires at 11:00 UTC (`GenericUtils/TimerUtils.cs`). Posts daily reports,
  updates the bot's activity status, and purges match events older than 90 days.

## Slash commands (`DiscordBot/DiscordBotSlashCommands.cs`, registered in `Program.cs`)
| Command | Purpose |
|---|---|
| `/mmr` | Current rank/MMR for a tracked player |
| `/addme` | Link a Riot ID to the caller's Discord account and start tracking |
| `/addchannel` | Subscribe the current channel to a player's updates |
| `/deletechannel` | Unsubscribe a channel (or delete the account) |
| `/deleteuser` | Admin (KickMembers): unlink a user |
| `/heatmap` | **Unfinished.** Owner-only. Select menus `match_selection` → `round_selection` → `kill_event_selection`, returns a placeholder image |

`DiscordBot/DiscordBotCommand.cs` (old prefix/text commands) is dead code. Its wiring in `Program.cs`
is commented out.

## HenrikDev API (`HenrikApi.cs`)
Endpoints: `v1/account/{name}/{tag}`, `v2/by-puuid/mmr/{region}/{puuid}`,
`v1/by-puuid/mmr-history/{region}/{puuid}`, `v3/by-puuid/matches/{region}/{puuid}?mode=&map=&size=`.
Rate limiting is passive. The client reads `x-ratelimit-*` headers and logs a warning at 60% used and
an error at 80%. It doesn't throttle. The 15-users-per-30s batch size is what keeps usage in budget.
Several methods block on async calls (`.Result`). See the backlog.

## Game content
Agents, maps, modes, ranks, and emojis are hardcoded enums with string, image-URL, and Discord-emoji
mappings in `Valorant/Enums/`. Episode/act date ranges are hardcoded in
`Valorant/Helpers/EpisodeActInfos.cs`. These go stale when Riot ships content. See the
`update-game-content` skill. `ValorantApp/Images/` holds icons copied to output, but current code
uses wiki image URLs and custom Discord emojis instead.
