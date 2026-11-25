#!/usr/bin/env python3
"""
Old-vs-new replay: run two versions of the bot on the same database copy and the same fake HenrikDev responses,
and diff what each would have posted to Discord. The strongest "does this change alter behavior?" check.

For each side (base ref, head ref or the current working tree) it:
  1. checks out the ref in a temporary git worktree (the working tree is used as-is for WORKTREE),
  2. builds it (Release) into a temp folder,
  3. copies the database, starts tools/replay/fake_henrik.py on a free port,
  4. runs the app in test mode with a frozen clock and manual commands (VALORANTAPP_MANUAL=1):
     enough "poll" commands to cover every tracked user, then "daily",
  5. collects the JSON-lines recording of every post.
Then it diffs the two recordings. Exit 0 = identical, 1 = different, 2 = harness error.

Both refs must support manual test mode (added on branch aiReady, 2026-11). Never point --db at the live
database: use a downloaded copy. Outputs contain real names from that database, so --out must be outside the repo.

Usage:
  python -I tools/replay/replay.py --base develop --db <copy of myDatabase.db> [--head WORKTREE] [--now ISO] [--out DIR]
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
        with self.cv:
            deadline = threading.Event()
            timer = threading.Timer(timeout, lambda: (deadline.set(), self._notify()))
            timer.start()
            try:
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


def run_side(name, ref, db, now, work):
    side = os.path.join(work, name)
    os.makedirs(side)
    if ref == "WORKTREE":
        src = REPO
    else:
        src = os.path.join(side, "src")
        run(["git", "-C", REPO, "worktree", "add", "--detach", src, ref])
    try:
        out_bin = os.path.join(side, "bin")
        print(f"[{name}] building {ref} ...", flush=True)
        run(["dotnet", "build", os.path.join(src, "ValorantApp", "ValorantApp.csproj"), "-c", "Release", "-o", out_bin, "--nologo", "-v", "q"])

        db_copy = os.path.join(side, "replay.db")
        shutil.copyfile(db, db_copy)
        record = os.path.join(side, "posts.jsonl")
        open(record, "w").close()

        fake = subprocess.Popen([sys.executable, "-I", os.path.join(REPO, "tools", "replay", "fake_henrik.py"),
                                 "--fixtures", FIXTURES, "--db", db_copy, "--now", now],
                                stdout=subprocess.PIPE, text=True)
        port = fake.stdout.readline().split()[1]

        env = {k: v for k, v in os.environ.items() if not k.startswith("VALORANTAPP_")}
        env.update({
            "VALORANTAPP_TESTMODE": "1",
            "VALORANTAPP_HENRIK_URL": f"http://127.0.0.1:{port}/valorant/",
            "VALORANTAPP_SQLITE_PATH": db_copy,
            "VALORANTAPP_RECORD_PATH": record,
            "VALORANTAPP_NOW": now,
            "VALORANTAPP_MANUAL": "1",
        })
        log = open(os.path.join(side, "stdout.log"), "w", encoding="utf-8")
        app = subprocess.Popen(["dotnet", os.path.join(out_bin, "ValorantApp.dll")], cwd=side, env=env,
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        errors = []
        try:
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
            if app.poll() is None:
                app.kill()
            fake.kill()
            log.close()
        with open(record, encoding="utf-8") as f:
            posts = [json.dumps(json.loads(l), sort_keys=True, ensure_ascii=False) for l in f if l.strip()]
        return posts, errors
    finally:
        if ref != "WORKTREE":
            subprocess.run(["git", "-C", REPO, "worktree", "remove", "--force", src], capture_output=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base", required=True, help="git ref for the old version (e.g. develop, main, a SHA)")
    ap.add_argument("--head", default="WORKTREE", help="git ref for the new version, or WORKTREE (default) for the current files")
    ap.add_argument("--db", required=True, help="a COPY of a prod myDatabase.db (outside the repo)")
    ap.add_argument("--now", help="frozen clock, ISO-8601 UTC (default: 11:30 UTC the day after the newest match in --db)")
    ap.add_argument("--out", help="output folder outside the repo (default: a temp folder)")
    args = ap.parse_args()

    if inside_repo(args.db):
        sys.exit("--db must be outside the repository (it holds real user data).")
    out = args.out or tempfile.mkdtemp(prefix="valorant-replay-")
    if inside_repo(out):
        sys.exit("--out must be outside the repository (outputs contain real names).")
    os.makedirs(out, exist_ok=True)
    now = args.now or default_now(args.db)
    print(f"Replay at {now} with {user_count(args.db)} tracked users. Output: {out}", flush=True)

    try:
        base_posts, base_err = run_side("base", args.base, args.db, now, out)
        head_posts, head_err = run_side("head", args.head, args.db, now, out)
    except Exception as e:
        print(f"HARNESS ERROR: {e}", file=sys.stderr)
        return 2

    diff = list(difflib.unified_diff(base_posts, head_posts, f"base ({args.base})", f"head ({args.head})", lineterm="", n=1))
    with open(os.path.join(out, "diff.txt"), "w", encoding="utf-8") as f:
        f.write("\n".join(diff))

    print(f"base: {len(base_posts)} posts, {len(base_err)} errors | head: {len(head_posts)} posts, {len(head_err)} errors")
    for e in base_err:
        print(f"  base {e}")
    for e in head_err:
        print(f"  head {e}")
    if not diff and base_err == head_err:
        print("IDENTICAL: the change does not alter any post.")
        return 0
    changed = sum(1 for l in diff if l.startswith("+") and not l.startswith("+++"))
    print(f"DIFFERENT: {changed} added/changed post lines. See {os.path.join(out, 'diff.txt')}")
    return 1


if __name__ == "__main__":
    sys.exit(main())
