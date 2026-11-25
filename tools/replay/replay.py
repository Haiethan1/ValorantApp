#!/usr/bin/env python3
"""
Old-vs-new replay: run two versions of the bot on the same database and the same fake HenrikDev responses, and diff
what each would have posted to Discord. Answers "does this change alter any post?".

For each side (base ref, head ref or the current working tree) it builds the app (Release), copies the database,
starts tools/replay/fake_henrik.py (every tracked user "just played" a recorded match), runs the app in test mode with
a frozen clock and manual commands (VALORANTAPP_MANUAL=1): enough "poll" commands to cover every tracked user, then
"daily". Then it diffs the two recordings.

Database:
  --db <copy>    a COPY of a prod myDatabase.db (outside the repo; real data, local runs only)
  --synthetic    a generated fake database (tools/replay/make_synthetic_db.py); used by CI

Exit codes: 0 identical (or skipped, or DIFFERENT with --ci), 1 different, 2 harness error.
--ci also writes a Markdown summary to $GITHUB_STEP_SUMMARY when set.

Both refs must support manual test mode (added 2026-11). If the base doesn't, the replay is skipped (exit 0).

Usage:
  python -I tools/replay/replay.py --base develop --db <copy of myDatabase.db> [--head WORKTREE] [--now ISO] [--out DIR]
  python -I tools/replay/replay.py --base origin/develop --synthetic --ci
"""
import argparse
import difflib
import json
import math
import os
import shutil
import sqlite3
import subprocess
import sys
import tempfile
import threading
from datetime import datetime, timedelta, timezone

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FIXTURES = os.path.join(REPO, "ValorantAppTests", "MockJsonResponse", "Recorded")
USERS_PER_TICK = 15
COMMAND_TIMEOUT_S = 300
SYNTHETIC_NOW = "2026-01-15T11:30:00Z"
SUPPORT_MARKER = "VALORANTAPP_MANUAL"


def ro_uri(path):
    """Read-only SQLite URI that also works for Windows paths."""
    return "file:" + os.path.abspath(path).replace(os.sep, "/") + "?mode=ro"


def run(cmd, **kw):
    r = subprocess.run(cmd, capture_output=True, text=True, **kw)
    if r.returncode != 0:
        raise RuntimeError(f"{' '.join(cmd)} failed:\n{r.stdout[-2000:]}\n{r.stderr[-2000:]}")
    return r.stdout


def inside_repo(path):
    return os.path.abspath(path).startswith(REPO + os.sep)


def ref_supports_replay(ref):
    if ref == "WORKTREE":
        path = os.path.join(REPO, "ValorantApp", "AppOptions.cs")
        return os.path.exists(path) and SUPPORT_MARKER in open(path, encoding="utf-8-sig").read()
    r = subprocess.run(["git", "-C", REPO, "show", f"{ref}:ValorantApp/AppOptions.cs"], capture_output=True, text=True)
    return r.returncode == 0 and SUPPORT_MARKER in r.stdout


def default_now(db):
    con = sqlite3.connect(ro_uri(db), uri=True)
    latest = con.execute("SELECT max(game_start_patched_utc) FROM Matches").fetchone()[0]
    con.close()
    day = datetime.fromisoformat(latest).date() + timedelta(days=1) if latest else datetime.now(timezone.utc).date()
    # 11:30 UTC: after the 11:00 daily report time, so "yesterday" is the last day of data in the copy.
    return f"{day.isoformat()}T11:30:00Z"


def user_count(db):
    con = sqlite3.connect(ro_uri(db), uri=True)
    n = con.execute("SELECT count(*) FROM ValorantUsers").fetchone()[0]
    con.close()
    return n


class Lines:
    """Reads a process's stdout on a thread so we can wait for specific lines with a timeout."""

    def __init__(self, stream, log):
        self.cv = threading.Condition()
        self.lines = []
        self.log = log
        threading.Thread(target=self._pump, args=(stream,), daemon=True).start()

    def _pump(self, stream):
        for line in stream:
            self.log.write(line)
            with self.cv:
                self.lines.append(line.rstrip("\n"))
                self.cv.notify_all()
        with self.cv:
            self.lines.append(None)
            self.cv.notify_all()

    def wait_for(self, predicate, timeout):
        start = 0
        deadline = threading.Event()
        timer = threading.Timer(timeout, lambda: (deadline.set(), self._notify()))
        timer.start()
        try:
            with self.cv:
                while True:
                    for i in range(start, len(self.lines)):
                        line = self.lines[i]
                        if line is None:
                            raise RuntimeError("process exited early")
                        if predicate(line):
                            del self.lines[: i + 1]
                            return line
                    start = len(self.lines)
                    if deadline.is_set():
                        raise RuntimeError("timed out")
                    self.cv.wait()
        finally:
            timer.cancel()

    def _notify(self):
        with self.cv:
            self.cv.notify_all()


def build(name, ref, work):
    """Builds a ref (or the working tree) into <work>/<name>/bin and returns that folder."""
    side = os.path.join(work, name)
    os.makedirs(side, exist_ok=True)
    src = REPO
    if ref != "WORKTREE":
        src = os.path.join(side, "src")
        run(["git", "-C", REPO, "worktree", "add", "--detach", src, ref])
    try:
        out_bin = os.path.join(side, "bin")
        print(f"[{name}] building {ref} ...", flush=True)
        run(["dotnet", "build", os.path.join(src, "ValorantApp", "ValorantApp.csproj"), "-c", "Release", "-o", out_bin, "--nologo", "-v", "q"])
        return out_bin
    finally:
        if ref != "WORKTREE":
            subprocess.run(["git", "-C", REPO, "worktree", "remove", "--force", src], capture_output=True)


def start_app(out_bin, cwd, db, record, now, henrik_url):
    env = {k: v for k, v in os.environ.items() if not k.startswith("VALORANTAPP_")}
    env.update({
        "VALORANTAPP_TESTMODE": "1",
        "VALORANTAPP_HENRIK_URL": henrik_url,
        "VALORANTAPP_SQLITE_PATH": db,
        "VALORANTAPP_RECORD_PATH": record,
        "VALORANTAPP_NOW": now,
        "VALORANTAPP_MANUAL": "1",
    })
    return subprocess.Popen(["dotnet", os.path.join(out_bin, "ValorantApp.dll")], cwd=cwd, env=env,
                            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)


def create_synthetic_db(out_bin, work, now):
    """Lets the base build create the schema in an empty file, then fills it with generated fake data."""
    folder = os.path.join(work, "synthetic")
    os.makedirs(folder, exist_ok=True)
    db = os.path.join(folder, "synthetic.db")
    log = open(os.path.join(folder, "schema.log"), "w", encoding="utf-8")
    app = start_app(out_bin, folder, db, os.path.join(folder, "unused.jsonl"), now, "http://127.0.0.1:9/valorant/")
    try:
        lines = Lines(app.stdout, log)
        lines.wait_for(lambda l: l.strip() == "READY", 120)
        app.stdin.write("quit\n")
        app.stdin.flush()
        app.wait(30)
    finally:
        if app.poll() is None:
            app.kill()
        log.close()
    run([sys.executable, "-I", os.path.join(REPO, "tools", "replay", "make_synthetic_db.py"), db, "--now", now])
    return db


def replay_side(name, out_bin, db, now, work):
    side = os.path.join(work, name)
    os.makedirs(side, exist_ok=True)
    db_copy = os.path.join(side, "replay.db")
    shutil.copyfile(db, db_copy)
    record = os.path.join(side, "posts.jsonl")
    open(record, "w").close()

    fake_log_path = os.path.join(side, "fake_henrik.log")
    fake_log = open(fake_log_path, "w", encoding="utf-8")
    fake = subprocess.Popen([sys.executable, "-I", os.path.join(REPO, "tools", "replay", "fake_henrik.py"),
                             "--fixtures", FIXTURES, "--db", db_copy, "--now", now],
                            stdout=subprocess.PIPE, stderr=fake_log, text=True)
    app = None
    log = None
    errors = []
    try:
        first = fake.stdout.readline().split()
        if len(first) < 2:
            fake.kill()
            fake.wait(10)
            fake_log.close()
            with open(fake_log_path, encoding="utf-8") as f:
                tail = f.read()[-2000:]
            raise RuntimeError(f"fake_henrik.py did not start (exit {fake.returncode}):\n{tail}")
        port = first[1]
        log = open(os.path.join(side, "stdout.log"), "w", encoding="utf-8")
        app = start_app(out_bin, side, db_copy, record, now, f"http://127.0.0.1:{port}/valorant/")
        lines = Lines(app.stdout, log)
        lines.wait_for(lambda l: l.strip() == "READY", 120)
        polls = math.ceil(user_count(db_copy) / USERS_PER_TICK) + 1
        for cmd in ["poll"] * polls + ["daily"]:
            app.stdin.write(cmd + "\n")
            app.stdin.flush()
            answer = lines.wait_for(lambda l: l.startswith(f"OK {cmd}") or l.startswith(f"ERR {cmd}") or l.startswith("ERR unknown"), COMMAND_TIMEOUT_S)
            if answer.startswith("ERR"):
                errors.append(answer)
        app.stdin.write("quit\n")
        app.stdin.flush()
        app.wait(30)
    finally:
        if app is not None and app.poll() is None:
            app.kill()
        fake.kill()
        if log is not None:
            log.close()
        fake_log.close()
    with open(record, encoding="utf-8") as f:
        posts = [json.loads(l) for l in f if l.strip()]
    return posts, errors


def post_lines(posts):
    """Pretty-printed posts, so a diff shows the changed field rather than a whole one-line post."""
    out = []
    for i, p in enumerate(posts):
        out.append(f"=== post {i + 1}: {p.get('type')} to channel {p.get('channelId')}: {p.get('title') or p.get('text')}")
        out.extend(json.dumps(p, sort_keys=True, indent=1, ensure_ascii=False).splitlines())
    return out


def write_summary(text):
    path = os.environ.get("GITHUB_STEP_SUMMARY")
    if path:
        with open(path, "a", encoding="utf-8") as f:
            f.write(text + "\n")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base", required=True, help="git ref for the old version (e.g. develop, origin/develop, a SHA)")
    ap.add_argument("--head", default="WORKTREE", help="git ref for the new version, or WORKTREE (default) for the current files")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--db", help="a COPY of a prod myDatabase.db (outside the repo)")
    src.add_argument("--synthetic", action="store_true", help="use a generated fake database (CI)")
    ap.add_argument("--now", help="frozen clock, ISO-8601 UTC")
    ap.add_argument("--out", help="output folder outside the repo (default: a temp folder)")
    ap.add_argument("--ci", action="store_true", help="exit 0 on DIFFERENT and write a GitHub step summary")
    args = ap.parse_args()

    if args.db and inside_repo(args.db):
        sys.exit("--db must be outside the repository (it holds real user data).")
    out = args.out or tempfile.mkdtemp(prefix="valorant-replay-")
    if args.db and inside_repo(out):
        sys.exit("--out must be outside the repository (outputs contain real names).")
    os.makedirs(out, exist_ok=True)

    if not ref_supports_replay(args.base):
        msg = f"SKIPPED: base `{args.base}` predates replay support (manual test mode), nothing to compare."
        print(msg)
        write_summary(f"### Replay\n{msg}")
        return 0

    try:
        base_bin = build("base", args.base, out)
        head_bin = build("head", args.head, out)
        if args.synthetic:
            now = args.now or SYNTHETIC_NOW
            db = create_synthetic_db(base_bin, out, now)
        else:
            now = args.now or default_now(args.db)
            db = args.db
        print(f"Replay at {now} with {user_count(db)} tracked users. Output: {out}", flush=True)
        base_posts, base_err = replay_side("base", base_bin, db, now, out)
        head_posts, head_err = replay_side("head", head_bin, db, now, out)
    except Exception as e:
        print(f"HARNESS ERROR: {e}", file=sys.stderr)
        write_summary(f"### Replay\nHarness error: `{str(e)[:500]}`")
        return 2

    diff = list(difflib.unified_diff(post_lines(base_posts), post_lines(head_posts),
                                     f"base ({args.base})", f"head ({args.head})", lineterm="", n=3))
    diff_path = os.path.join(out, "diff.txt")
    with open(diff_path, "w", encoding="utf-8") as f:
        f.write("\n".join(diff))

    base_json = [json.dumps(p, sort_keys=True) for p in base_posts]
    head_json = [json.dumps(p, sort_keys=True) for p in head_posts]
    changed = sum(1 for p in head_json if p not in base_json) + max(0, len(base_json) - len(head_json))
    stats = f"base: {len(base_posts)} posts, {len(base_err)} errors | head: {len(head_posts)} posts, {len(head_err)} errors"
    print(stats)
    for e in base_err:
        print(f"  base {e}")
    for e in head_err:
        print(f"  head {e}")

    identical = not diff and base_err == head_err
    verdict = "IDENTICAL: the change does not alter any post." if identical else f"DIFFERENT: about {changed} posts changed. See {diff_path}"
    print(verdict)
    source = "synthetic database" if args.synthetic else "database copy"
    summary = [f"### Replay vs `{args.base}` ({source}, clock {now})", "", f"**{verdict.split(':')[0]}**: {stats}", ""]
    if head_err != base_err:
        summary += ["Errors (head):", *[f"- `{e}`" for e in head_err], ""]
    if not identical:
        shown = "\n".join(diff[:300])
        summary += ["<details><summary>Diff (first 300 lines; full diff in the replay artifact)</summary>", "",
                    "```diff", shown, "```", "</details>"]
    write_summary("\n".join(summary))

    if args.ci:
        # Changed posts are for a human to judge; new errors in head (the app threw on poll/daily) are a failure.
        new_errors = [e for e in head_err if e not in base_err]
        return 1 if new_errors else 0
    return 0 if identical else 1


if __name__ == "__main__":
    sys.exit(main())
