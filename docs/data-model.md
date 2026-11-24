# Data model

## Two databases (partially migrated)
| | SQLite | SQL Server |
|---|---|---|
| Where | `myDatabase.db` in the app's working dir (Pi: `<app-dir>`) | Docker on the NAS (`<nas-host>`), DB `Valorant`. Separate dev and prod containers (see docs/hosting.md) |
| Connection | `ConnectionStrings["Database"]` | `ConnectionStrings["DatabaseDev" \| "DatabaseProd"]` by `Debugger.IsAttached` (`Database/Extensions/BaseTable.cs`) |
| Role | **Source of truth for all reads.** Original DB. | Receives **dual writes** of core tables. **Only** store for heatmap tables. Never read for core features. |
| Schema created by | App at startup (`CREATE TABLE IF NOT EXISTS`) + manual scripts in `db/sqlite/` | Manual: `db/sqlserver/2024-11_schema.sql`. No auto-creation. |
| Backups | Manual copy to the dev PC (`<backup-dir>/DB/`) | **None yet** |

**Why SQL Server exists:** heatmap data (per-kill events and player positions) caused SQLite
performance problems in late 2024. The SQL Server migration started but wasn't finished, so core
reads never moved. The SQLite problems might also be solvable by batching writes in a transaction.
That decision is still open; see the backlog.

## Table name mapping
| Concept | SQLite | SQL Server | Model (`Database/Tables/`) | DAO (`Database/Extensions/`) |
|---|---|---|---|---|
| Tracked users (Riot ↔ Discord) | `ValorantUsers` | `lu_valorant_users` | `ValorantUsers` | `ValorantUsersExtension` |
| Channel subscriptions | `ValorantChannelMapping` | `lu_valorant_channel_mappings` | `ValorantChannelMappings` | `ValorantChannelMappingsExtension` |
| Match metadata | `Matches` | `lu_matches` | `Matches` | `MatchesExtension` |
| Per-player match stats (tracked users) | `MatchStats` | `lu_match_stats` | `MatchStats` | `MatchStatsExtension` |
| All players seen (heatmap) | (script exists, unused) | `lu_valorant_players` | `ValorantPlayers` | `ValorantPlayersExtension` |
| Players in a match + agent (heatmap) | (script exists, unused) | `lu_match_players` | `MatchPlayers` | `MatchPlayersExtension` |
| Kill and other round events (heatmap) | (script exists, unused) | `lu_match_events` | `MatchEvents` | `MatchEventsExtension` |
| Player x/y at each event (heatmap) | (script exists, unused) | `lu_player_locations` | `PlayerLocations` | `PlayerLocationsExtension` |
| Query retry log | `QueryLog`? | `lu_query_log` | n/a | `GenericExtensions/SqlExtensions.cs` |

Whether the Nov-2024 SQLite heatmap tables were ever applied on the Pi is unconfirmed. Verify against
a copy of the prod DB before relying on them.

## Persisted enums: append only
`MatchPlayers.agent` stores `(byte)Agents`. `team` stores `TeamEnum`, and `event_type` stores
`EventTypeEnum`. Reordering or inserting enum members silently corrupts historical rows.

## Migrations
No migration framework. Scripts in `db/` are applied **by hand** in filename order:
- `db/sqlite/`: applied to the Pi's `myDatabase.db`. Copy the scripts to `<release-scripts-dir>`
  on the Pi, stop the app, run with `sqlite3`, restart (owner only; see the `deploy-pi` skill).
- `db/sqlserver/`: applied with SSMS (or `sqlcmd`) against the dev instance first, then prod.
- `db/queries/`: ad-hoc reporting queries, not migrations (e.g. year-end stats).

When changing schema:
1. Add a new dated script under `db/sqlite/` and/or `db/sqlserver/`. Never edit applied scripts.
2. Update the `CREATE TABLE IF NOT EXISTS` DDL in the DAO so fresh databases match.
3. Update the model in `Database/Tables/` and the DAO queries for **both** databases if dual-written.
4. Test against a copy of the prod DB (`getValDB`), never the live file.
