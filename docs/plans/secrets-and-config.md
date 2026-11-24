# Plan: secrets & configuration redesign

**Status:** proposed, not started. Discussed Nov 2026; to be picked up in a dedicated session.

## Problem
- Secrets (bot tokens, HenrikDev key, SQL Server logins) live in `ValorantApp/App.config`. It's
  committed with blank values and kept out of commits locally via `git update-index --assume-unchanged`.
  That's fragile, and it puts secrets inside the working tree where tools and agents can read them.
- The build copies App.config to `ValorantApp.dll.config`, so **secrets ship with every deploy**
  (`scp -r *` overwrites the Pi's config) and end up in local release backups.
- Dev vs prod is chosen by `Debugger.IsAttached`, so `dotnet run` silently uses **prod** credentials.

## Proposed design
Replace `System.Configuration.ConfigurationManager` + `App.config` with
`Microsoft.Extensions.Configuration` layers (later overrides earlier):

| Layer | Contents | Location | In git |
|---|---|---|---|
| `appsettings.json` | non-secret defaults (SQLite path, HenrikDev base URL) | repo | yes |
| `appsettings.{Env}.json` | non-secret per-env values (SQL Server host:port) | repo | yes |
| Dev: `dotnet user-secrets` | dev bot token, Henrik key, dev SQL login | `%APPDATA%\Microsoft\UserSecrets\<id>\` | no |
| Prod: secrets file | prod bot token, Henrik key, prod SQL login | Pi, outside the app dir, e.g. `~/.config/valorantapp/secrets.json` (`chmod 600`) | no |
| Environment variables | optional overrides, `VALORANTAPP_` prefix (`VALORANTAPP_Discord__BotToken`) | shell / future systemd unit | no |

- `VALORANTAPP_ENV` = `Development` | `Production` replaces `Debugger.IsAttached`. Proposed default
  when unset: `Development`, so prod has to opt in. The Pi start alias becomes
  `VALORANTAPP_ENV=Production nohup ./ValorantApp &`.
- One key per secret, with the environment deciding the source: `Discord:BotToken`, `Henrik:ApiKey`,
  `ConnectionStrings:Sqlite`, `ConnectionStrings:SqlServer`. This drops `DevelopBotToken` and
  `DatabaseDev`/`DatabaseProd`.
- Fail fast at startup if a required key is missing. Log the key name, never the value.
- A static `AppConfig` built once in `Main`. It fits the static DAO design without a DI refactor.

### Code touchpoints (as of Nov 2026)
- `Program.cs:30` SQLite connection string, `:70` HenrikToken header, `:105` bot token
- `Database/Extensions/BaseTable.cs:8-9` SQLite + SQL Server connection strings
- `ValorantApp.csproj`: add `Microsoft.Extensions.Configuration.{Json,EnvironmentVariables,UserSecrets}`
  (9.0.x, matching existing `Microsoft.Extensions.*`), add `UserSecretsId`, copy `appsettings*.json` to output,
  remove `System.Configuration.ConfigurationManager` + `App.config`
- Docs: `AGENTS.md`, `README.md`, `docs/hosting.md`, `.claude/skills/deploy-pi`

## Migration (one time)
1. Dev: `git update-index --no-assume-unchanged ValorantApp/App.config`. The owner runs
   `dotnet user-secrets set ...` for each key (values never pass through an agent), then App.config
   is deleted.
2. Pi: the owner creates the prod secrets file from a template, deploys the new build once, updates
   the `valappnohup` alias, and deletes the old `ValorantApp.dll.config`.
3. Clean old secret copies out of local backups (`<backup-dir>/releaseFiles/*/ValorantApp.dll.config`,
   any `App.config.temp` copies in `<backup-dir>` and `ValorantApp/`).
4. **Rotate all credentials** while re-entering them: Discord bot tokens (dev + prod), HenrikDev key,
   SQL Server login passwords (dev + prod).

## Open decisions
1. Default env when `VALORANTAPP_ENV` is unset: `Development` (proposed) vs refuse to start.
2. Prod secrets: JSON file (proposed; works with the current `nohup` setup) vs env vars in a start script.
3. Rotate credentials during the migration (recommended).
4. Scope: config only (proposed), or also move to the .NET Generic Host (`Host.CreateApplicationBuilder`)
   for built-in config, environment, logging and graceful shutdown. Possibly combine with the systemd
   unit from the backlog.
