# AGENTS.md: ValorantApp ("shamebot")

Instructions for AI coding agents (Claude Code, Antigravity/Gemini, etc.). `CLAUDE.md` imports this file.
Read the linked docs before non-trivial changes. They hold the context that isn't obvious from the code.

## What this is
A 24/7 .NET 8 console app that polls the HenrikDev Valorant API for tracked players, stores match
stats, and posts match recaps and daily reports to Discord channels. It's a personal project for a
small friend group (~30 tracked players). It runs in **production on a Raspberry Pi 4** and is live,
so treat prod data and deploys with care.

- Architecture & runtime flow: [docs/architecture.md](docs/architecture.md)
- Databases, schema, migration history: [docs/data-model.md](docs/data-model.md)
- Dev/prod hosting, deploy, logs, backups: [docs/hosting.md](docs/hosting.md)
- Testing and validation (what exists, safety rules, how to write tests): [docs/testing.md](docs/testing.md)
- Known issues, half-finished features, tech debt, planned work: [docs/backlog.md](docs/backlog.md)
- Design plans for upcoming work: [docs/plans/](docs/plans/)

## Commands
```bash
dotnet build                      # from repo root (ValorantApp.sln)
dotnet test                       # MSTest + Moq; MatchTest_Premier_Success is quarantined with [Ignore] (see backlog)
dotnet run --project ValorantApp  # needs a filled-in App.config (see Secrets); connects to Discord!
dotnet publish ValorantApp -p:PublishProfile=FolderProfile   # linux-arm64 self-contained build for the Pi
```
Running the app logs into Discord with a real bot token and starts polling. Never run it with the
**prod** token from a dev machine (two instances would double-post). Dev vs prod selection is explained below.

## Layout
```
ValorantApp/
  Program.cs                 entry point, DI wiring, Discord login, slash-command registration
  Valorant/BaseValorantProgram.cs   core engine: polling loop, match processing, daily reports
  Valorant/BaseValorantUser.cs      a tracked player (wraps HenrikApi)
  Valorant/Enums/            Agents, Maps, Modes, ranks, emojis (game content lives here)
  Valorant/Helpers/          EpisodeActInfos (hardcoded episode/act dates), constants
  HenrikApi.cs, HenrikJson/  HenrikDev API client + JSON models
  DiscordBot/                DiscordBotSlashCommands.cs (live); DiscordBotCommand.cs (dead text commands)
  Database/Tables/           row models; Database/Extensions/ static DAO classes (raw ADO.NET SQL)
  GenericExtensions/, GenericUtils/  helpers (SQL retry, HTTP logging, timers)
ValorantAppTests/            MSTest: unit, component, and end-to-end tests; JSON fixtures in MockJsonResponse/
db/                          SQL migration history (SQLite + SQL Server), applied by hand
docs/                        project knowledge for humans and agents
```

## Critical rules / gotchas
- **No direct prod access.** Never SSH to the Pi or connect to the NAS SQL Server yourself. When you
  need prod logs, data, or state, give the user exact commands to run and ask for the output. Work on
  copies (e.g. a downloaded `myDatabase.db`), never live data.
- **Host-specific values** (IPs, paths, ports, aliases) are in `docs/hosting.local.md`, which is
  gitignored and exists only on the owner's machine. `docs/hosting.md` uses `<placeholders>` for them.
  Never copy those values into committed files (public repo). Cloud agents won't have the file and
  don't need it.
- **Dev vs prod is `Debugger.IsAttached`.** With a debugger attached: `DevelopBotToken` + `DatabaseDev`.
  Without one (including `dotnet run`): `BotToken` + `DatabaseProd`. There is no env-var switch yet.
- **Secrets live in `ValorantApp/App.config`, which is committed with blank values** and marked
  `git update-index --assume-unchanged` locally so real values aren't committed. **Never commit
  real values, and never print them.** If you must edit App.config structure, coordinate with the user.
  Moving to a proper secrets mechanism is planned (backlog).
- **Enums are persisted as integers** (e.g. `Agents` → `MatchPlayers.agent TINYINT`, `TeamEnum`,
  `EventTypeEnum`). **Only append new members at the end, before nothing else. Never reorder,
  insert, or delete.** (`Maps` and `Modes` are stored as text, but keep the same discipline.)
- **Two databases, half-migrated.** SQLite (`myDatabase.db`, next to the binary) is the source of
  truth for reads. SQL Server (on the NAS) receives dual writes plus heatmap-only tables.
  A schema change usually needs SQLite DDL, SQL Server DDL, a migration script in `db/`, and model
  and DAO updates. See [docs/data-model.md](docs/data-model.md).
- **HenrikDev rate limit** (~90 req/min on the key). The poller does 15 users per 30s tick. Don't add
  per-user API calls inside loops without accounting for the budget.
- **Slash commands are registered globally** on every startup (`BulkOverwriteGlobalApplicationCommandsAsync`).
  Global command changes can take time to propagate in Discord.
- Timezone handling: stored match times are UTC. Daily report fires at 11:00 UTC.
- Some heatmap commands are gated to the owner's Discord ID (hardcoded). They're unfinished.

## Conventions
- Branches & commits: `VAL-<github issue #>` (e.g. branch `VAL-64`, commit `VAL-64: Added ...`).
  Non-issue work uses a descriptive branch name as prefix (e.g. `presentationReady: ...`).
- Flow: feature branch → PR into `develop` → `develop` merged into `main` for releases.
- C# style: existing code uses file-scoped classes in block namespaces, `PascalCase` public members,
  static extension classes for DAO and enum helpers. Match the surrounding style. No reformatting churn.
- Backlog lives in GitHub issues (`gh issue list`).

## Common workflows
- New agent / map / game mode / episode-act → skill `update-game-content` (`.claude/skills/`).
- Deploying to the Pi → skill `deploy-pi` (user-triggered only, never deploy on your own).
- Validating a change: build, `dotnet test`, then (for behavior changes) run against the **dev** bot/Discord server
  with a debugger attached (see docs/hosting.md#development). How tests are organized, the safety rules (tests can
  never reach SQL Server or prod), and how to write them: [docs/testing.md](docs/testing.md).
- Re-triggering a recap on the dev bot: the dev-only slash command `/devdeletematch` (registered only with a
  debugger attached, bot-owner only) deletes your latest stored match from SQLite and SQL Server so the poller
  re-posts it on the next tick. Logic lives in `Database/Extensions/DevToolsExtension.cs`.
