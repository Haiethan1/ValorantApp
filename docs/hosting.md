# Hosting, deployment & operations

This file describes **how** dev and prod are set up. Concrete hosts, paths, ports, and the owner's
shell aliases are in **`docs/hosting.local.md`**. That file is gitignored, so it exists only on the
owner's machine. If it's missing (cloud agent or CI), you don't need it: deploys and anything
touching prod are owner-only tasks.

## Prod access policy
AI agents never SSH to the Pi or connect to the NAS SQL Server themselves. When prod data or state
is needed (logs, a DB copy, process status), give the owner the exact commands to run and ask them
to paste the output, or tell them where to save the files. Work on copies, never live data.

## Machines (home LAN)
| Machine | Placeholder | Role |
|---|---|---|
| Dev PC (Windows 11, Git Bash) | n/a | Development, builds, publish. Keeps backups in `<backup-dir>` and logs in `<logs-dir>` |
| Raspberry Pi 4 (4 GB), headless | `<pi-host>` | **Production bot.** App runs from `<app-dir>` on an external drive |
| UGREEN NAS (UGOS) | `<nas-host>` | SQL Server 2022 (`mcr.microsoft.com/mssql/server:2022-latest`): two standalone Docker containers (dev `<sql-dev-container>`:`<sql-dev-port>`, prod `<sql-prod-container>`:`<sql-prod-port>`), managed in the UGOS Docker UI. No compose file. Volume mappings aren't recorded |

### How the bot runs on the Pi
The bot is started by hand over SSH and runs in the background under `nohup`. There's **no service
manager**: after a reboot or crash it stays down until the owner restarts it.
- **Start:** `cd <app-dir> && nohup ./ValorantApp &` (console output goes to `<app-dir>/nohup.out`)
- **Status:** `pgrep -af ValorantApp`
- **Stop:** `pkill -f ./ValorantApp` (SIGTERM), then confirm with `pgrep`

## Environments
| | Dev | Prod |
|---|---|---|
| Selected when | Debugger attached (Visual Studio / Rider F5) | No debugger (published binary, or `dotnet run`!) |
| Discord bot | `DevelopBotToken`: separate dev bot in a private test server | `BotToken`: the live bot in the friend-group servers |
| SQLite | `myDatabase.db` in the local build output folder, seeded with the owner's own user | `<app-dir>/myDatabase.db` |
| SQL Server | dev container (`DatabaseDev`) | prod container (`DatabaseProd`) |

⚠️ `dotnet run` without a debugger selects the **prod** token and prod SQL Server. This is planned
to change; see [plans/secrets-and-config.md](plans/secrets-and-config.md).

## Development
1. Fill in `ValorantApp/App.config` locally (blank in git). Keep it out of commits:
   `git update-index --assume-unchanged ValorantApp/App.config`.
2. Run under the debugger. The dev bot connects to the dev Discord server.
3. The SQLite DB is created in the working directory (`bin/Debug/net8.0/`) if missing. Add yourself
   with `/addme` and map a channel with `/addchannel` in the dev server.
4. To reproduce prod behavior, use a copy of a prod DB backup in the output folder.

Cloud agents and CI can `dotnet build` and `dotnet test` with no secrets (tests mock HTTP), but
can't run the bot.

## Deploy to production (manual, owner only)
The procedure, with concrete commands, is the `deploy-pi` skill. Overview:
1. Check out the release commit (normally `main`). Confirm App.config holds the **prod** values.
2. `dotnet publish ValorantApp -p:PublishProfile=FolderProfile`. Output goes to
   `ValorantApp/bin/Release/net8.0/publish/linux-arm64/` (self-contained, so the Pi doesn't need .NET).
   App.config becomes `ValorantApp.dll.config`, **which is how secrets currently reach the Pi**.
3. Back up the release folder and the prod `myDatabase.db` to `<backup-dir>`.
4. Stop the bot on the Pi.
5. `scp` the files to `<app-dir>`: everything for dependency/runtime changes, `ValorantApp*` only
   for code-only changes.
6. Apply any new `db/` migration scripts (see [data-model.md](data-model.md#migrations)).
7. Start the bot and `tail -f logs.txt` until you see a clean startup and the first poll (~1 min).

## Logs
In `<app-dir>`: `nohup.out` (console, grows unbounded), `logs.txt` (app) and `apilogs.txt`
(HenrikDev HTTP), rolling at 10 MB (`logs_001.txt`, …), with 5 files of each kept. The owner copies them to
`<logs-dir>` with scp.

## Backups
- SQLite: manual copy of `<app-dir>/myDatabase.db` to `<backup-dir>/DB/<date>.db`. No schedule.
- Release binaries: copied to `<backup-dir>/releaseFiles/<date>/` before each deploy.
- SQL Server: **none** (data lives only in the NAS Docker volumes).
- `<backup-dir>` also holds owner artifacts: year-end stats scripts, a post-match-analysis
  design diagram, and an embed prototype.
