# Valorant Discord Match Tracker ("shamebot")

A Discord bot that tracks Valorant matches for a group of friends, stores their stats, and posts
match recaps, rank changes, and daily summaries to Discord servers. It's been running 24/7 on a
Raspberry Pi since 2023.

## Features
- `/addme` links your Riot ID; `/addchannel` subscribes a channel to your matches
- Automatic match polling (HenrikDev API) with recap embeds: K/D/A, headshot %, multikills, RR change,
  and roast-worthy stats
- Rank promotion/demotion announcements and daily reports (11:00 UTC)
- `/mmr` for current rank
- Multi-server support: any number of Discord servers and channels per player
- (WIP) `/heatmap`: per-round kill positions

## Quick start (development)
Requirements: .NET 8 SDK, a Discord bot token (separate **dev** bot recommended), a
[HenrikDev API key](https://docs.henrikdev.xyz).

```bash
git clone git@github.com:Haiethan1/ValorantApp.git && cd ValorantApp
git update-index --assume-unchanged ValorantApp/App.config   # keep your secrets out of git
# fill in ValorantApp/App.config: DevelopBotToken, HenrikToken (and BotToken / SQL Server strings if needed)
dotnet build
dotnet test
```
Run from Visual Studio/Rider **with the debugger attached**. That selects the dev bot token and dev
database. Running without a debugger (including `dotnet run`) uses the **production** settings.
The SQLite database (`myDatabase.db`) is created automatically in the working directory.

## How it works
```
Discord ◄──► ValorantApp (.NET 8 on Raspberry Pi 4) ──► HenrikDev Valorant API
                 │  SQLite (primary)
                 └─► SQL Server on NAS (Docker; dual-write + heatmap data)
```
Every 30 s the poller checks 15 tracked players (round-robin) for new matches. That keeps it
inside HenrikDev's rate limit while leaving headroom for slash commands. With ~30 players, a match
is usually posted within about 2 minutes of ending.

More detail:
- [docs/architecture.md](docs/architecture.md): components, runtime loops, commands, API usage
- [docs/data-model.md](docs/data-model.md): SQLite vs SQL Server, schema, migrations (`db/`)
- [docs/hosting.md](docs/hosting.md): dev/prod environments, deploying to the Pi, logs, backups
- [docs/backlog.md](docs/backlog.md): known issues and tech debt

## Tech stack
C# / .NET 8 · Discord.Net · Microsoft.Data.Sqlite · Microsoft.Data.SqlClient · Serilog ·
OpenTelemetry · MSTest + Moq · deployed self-contained `linux-arm64` to a Raspberry Pi 4.

## Working on this repo with AI
Agent instructions live in [AGENTS.md](AGENTS.md) (imported by `CLAUDE.md`). Repeatable workflows are
skills in `.claude/skills/`: `update-game-content` (new agents/maps/modes/acts) and `deploy-pi`.

## Contributing
Branches and commits are prefixed with the GitHub issue: `VAL-<issue#>` (e.g. `VAL-64: Added ...`).
Feature branches → PR into `develop` → `develop` merged into `main` for releases.

## Notes
I definitely learned a lot with this. From database table design choices to setting up a service
from scratch, it was a fun ride. The community we've built around Valorant and this match tracker
really fueled me to work hard on it and keep improving it. The hype around Valorant has died down
for our group, but this match tracker will (hopefully) remain in the hearts of many.

The original codebase (2023–2025) was written without AI. Since late 2026 the project has been
revived with AI-assisted development.
