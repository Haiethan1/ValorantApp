---
name: deploy-pi
description: Publish ValorantApp and deploy it to the production Raspberry Pi. User-triggered only.
disable-model-invocation: true
---

# Deploy to the production Raspberry Pi

Production is live for real users. **Only run this when the user explicitly asks to deploy**, and
confirm before each step that touches the Pi (stop, copy, migrate, start).
Background: `docs/hosting.md`.

## Before anything
- Read `docs/hosting.local.md` and substitute its values for the `<placeholders>` below.
  **If that file doesn't exist, stop.** You're not on the owner's machine, and deploying isn't
  possible from here.
- **You have no access to the Pi.** The user runs every Pi-side command. Give exact commands one step
  at a time and wait for confirmation or pasted output. Local steps (publish, backup copy) you may
  run once the user approves the deploy.

## Preconditions
- On the release commit (normally `main` after merging `develop`), with `git status` clean
  (App.config is assume-unchanged, so it won't show).
- `dotnet build` and `dotnet test` pass (except known failures listed in `docs/backlog.md`).
- The user confirms that `ValorantApp/App.config` holds the **prod** values. Don't open it yourself.

## Steps
1. **Publish:** `dotnet publish ValorantApp -p:PublishProfile=FolderProfile`
   → `ValorantApp/bin/Release/net8.0/publish/linux-arm64/`.
2. **Back up the release** locally: copy the publish folder to `<backup-dir>/releaseFiles/<mon_dd_yyyy>/`.
3. **Back up the prod DB** (user runs): `scp <pi-host>:<app-dir>/myDatabase.db <backup-dir>/DB/<mon_dd_yyyy>.db`.
4. **Stop the bot** (user runs): `ssh <pi-host>`, then `pkill -f ./ValorantApp`, then `pgrep -af ValorantApp`
   should print nothing.
5. **Copy files** (user runs, from the publish folder):
   code-only change → `scp -r ValorantApp* <pi-host>:<app-dir>`;
   dependency/runtime/content changes → `scp -r * <pi-host>:<app-dir>`.
6. **Migrations** (only if the release includes new `db/sqlite/` scripts): copy them to
   `<release-scripts-dir>`, stop the bot, and apply in filename order with
   `sqlite3 <app-dir>/myDatabase.db < script.sql`. SQL Server scripts are applied via SSMS (dev first, then prod).
7. **Start the bot** (user runs): `cd <app-dir> && nohup ./ValorantApp &`, then `tail -f logs.txt`
   for a clean startup (Discord connected, commands registered, first poll ~1 min later).
8. Report what was deployed (commit SHA) and where the backups are.

## Rollback
Stop the bot, copy the previous release from `<backup-dir>/releaseFiles/<date>/` back to `<app-dir>`,
restore the DB backup if a migration ran, start the bot.
