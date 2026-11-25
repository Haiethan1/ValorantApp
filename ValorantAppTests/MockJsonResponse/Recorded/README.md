# Recorded HenrikDev responses

Real API responses recorded on 2026-11-24, scrubbed of all player identities by
`tools/fixtures/scrub_henrik_fixtures.py` (fake puuids `aaaaaaaa-...`, match ids `bbbbbbbb-...`, party ids
`cccccccc-...`, names `PlayerNN#TAGNN`). The tracked player is `Player01#TAG01`,
puuid `aaaaaaaa-0000-4000-8000-000000000001`.

| File | Endpoint | Notes |
|---|---|---|
| `account.json` | `v1/account/{name}/{tag}` | |
| `account_notfound.json` | same, unknown player | 404 error envelope (`errors[]`, no `status`/`data`) |
| `mmr.json` | `v2/by-puuid/mmr/{affinity}/{puuid}` | |
| `mmr_history.json` | `v1/by-puuid/mmr-history/{affinity}/{puuid}` | last 10 competitive games, losses and gains |
| `matches_competitive.json` | `v3/by-puuid/matches/...?mode=competitive&size=1` | also what the poller sees (`size=1`) |
| `matches_competitive_overtime.json` | from `size=10` | 26 rounds, 14-12 |
| `matches_competitive_shortgame.json` | from `size=10` | 13 rounds, 0-13 |
| `matches_deathmatch.json`, `_premier`, `_swiftplay`, `_unrated` | `?mode=<mode>&size=1` | |
| `matches_unavailable.json` | `?mode=newmap&size=1` | entry with `metadata: null` (`is_available` false) |
| `matches_empty.json` | `?mode=custom&size=1` | `data: []` |

Modes with no recent games (teamdeathmatch, spikerush, escalation, replication, snowball) returned the
deathmatch instead, so they're not stored.

Refresh (owner only, raw files stay outside the repo):
```bash
bash tools/fixtures/fetch_henrik_fixtures.sh <name> <tag> <puuid>
python -I tools/fixtures/scrub_henrik_fixtures.py ~/henrik-fixtures-raw <scratch-dir> --owner-puuid <puuid>
# copy the files listed above from <scratch-dir>, then run dotnet test (RecordedFixtureContractTests)
```
