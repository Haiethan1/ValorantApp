#!/usr/bin/env python3
"""
Fake HenrikDev API for replays (tools/replay/replay.py). Serves the scrubbed recorded fixtures in
ValorantAppTests/MockJsonResponse/Recorded/ so that EVERY tracked user in the given SQLite database "just played"
a match:

- matches:     one recorded match per user (chosen by a stable hash of the puuid), with the recorded tracked player
               (Player01#TAG01) replaced by that user's puuid/name/tag, a per-user deterministic match id, and a
               start time 30 minutes before --now.
- mmr-history: the recorded history with its newest entry pointing at that user's match.
- mmr:         the recorded MMR with the user's name/tag.
Everything else returns HenrikDev's 404 error envelope. Prints "LISTENING <port>" once ready.

Usage: python -I fake_henrik.py --fixtures <dir> --db <sqlite> --now 2025-12-29T10:30:00Z [--port 0]
"""
import argparse
import copy
import hashlib
import json
import os
import re
import sqlite3
import sys
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

OWNER_PUUID = "aaaaaaaa-0000-4000-8000-000000000001"
OWNER_NAME, OWNER_TAG = "Player01", "TAG01"
MATCH_FILES = [
    "matches_competitive.json", "matches_competitive_overtime.json", "matches_competitive_shortgame.json",
    "matches_deathmatch.json", "matches_premier.json", "matches_swiftplay.json", "matches_unrated.json",
]
NOT_FOUND = json.dumps({"errors": [{"code": 22, "message": "Not found", "status": 404, "details": None}]}).encode()


def ro_uri(path):
    """Read-only SQLite URI that also works for Windows paths."""
    return "file:" + os.path.abspath(path).replace(os.sep, "/") + "?mode=ro"


def replace_values(o, mapping):
    """Exact-value replacement over the JSON tree (never substring: Player01 vs Player010)."""
    if isinstance(o, dict):
        return {k: replace_values(v, mapping) for k, v in o.items()}
    if isinstance(o, list):
        return [replace_values(v, mapping) for v in o]
    if isinstance(o, str) and o in mapping:
        return mapping[o]
    return o


class Fixtures:
    def __init__(self, fixtures_dir, db_path, now):
        self.now = now
        self.matches = {}
        for f in MATCH_FILES:
            with open(os.path.join(fixtures_dir, f), encoding="utf-8") as fh:
                self.matches[f] = json.load(fh)
        with open(os.path.join(fixtures_dir, "mmr_history.json"), encoding="utf-8") as fh:
            self.mmr_history = json.load(fh)
        with open(os.path.join(fixtures_dir, "mmr.json"), encoding="utf-8") as fh:
            self.mmr = json.load(fh)
        con = sqlite3.connect(ro_uri(db_path), uri=True)
        self.users = {p.lower(): (n, t) for p, n, t in con.execute("SELECT val_puuid, val_username, val_tagname FROM ValorantUsers")}
        con.close()

    def identity(self, puuid):
        name, tag = self.users.get(puuid, ("Unknown", "000"))
        return {OWNER_PUUID: puuid, OWNER_NAME: name, OWNER_TAG: tag, f"{OWNER_NAME}#{OWNER_TAG}": f"{name}#{tag}"}

    def fixture_for(self, puuid):
        h = int(hashlib.sha256(puuid.encode()).hexdigest(), 16)
        return MATCH_FILES[h % len(MATCH_FILES)]

    def match_id_for(self, puuid):
        return str(uuid.uuid5(uuid.NAMESPACE_URL, f"replay/{puuid}/{self.fixture_for(puuid)}"))

    def matches_body(self, puuid):
        fixture = self.matches[self.fixture_for(puuid)]
        mapping = self.identity(puuid)
        original_id = fixture["data"][0]["metadata"]["matchid"]
        mapping[original_id] = self.match_id_for(puuid)
        doc = replace_values(copy.deepcopy(fixture), mapping)
        start = self.now - timedelta(minutes=30)
        meta = doc["data"][0]["metadata"]
        meta["game_start"] = int(start.timestamp())
        meta["game_start_patched"] = start.strftime("%A, %B %d, %Y %I:%M %p").replace(" 0", " ")
        return doc

    def mmr_history_body(self, puuid):
        doc = replace_values(copy.deepcopy(self.mmr_history), self.identity(puuid))
        if doc.get("data"):
            doc["data"][0]["match_id"] = self.match_id_for(puuid)
        return doc

    def mmr_body(self, puuid):
        return replace_values(copy.deepcopy(self.mmr), self.identity(puuid))


ROUTES = [
    (re.compile(r"/v3/by-puuid/matches/[^/]+/([0-9a-fA-F-]{36})"), "matches_body"),
    (re.compile(r"/v1/by-puuid/mmr-history/[^/]+/([0-9a-fA-F-]{36})"), "mmr_history_body"),
    (re.compile(r"/v2/by-puuid/mmr/[^/]+/([0-9a-fA-F-]{36})"), "mmr_body"),
]


def make_handler(fx):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            for pattern, method in ROUTES:
                m = pattern.search(self.path)
                if m:
                    puuid = m.group(1).lower()
                    if puuid not in fx.users:
                        break
                    body = json.dumps(getattr(fx, method)(puuid), separators=(",", ":")).encode()
                    self._send(200, body)
                    return
            self._send(404, NOT_FOUND)

        def _send(self, code, body):
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *args):
            pass

    return Handler


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--fixtures", required=True)
    ap.add_argument("--db", required=True)
    ap.add_argument("--now", required=True)
    ap.add_argument("--port", type=int, default=0)
    args = ap.parse_args()
    now = datetime.fromisoformat(args.now.replace("Z", "+00:00")).astimezone(timezone.utc)
    fx = Fixtures(args.fixtures, args.db, now)
    # The bot fires up to 15 requests at once; the default listen backlog (5) refuses some of them.
    ThreadingHTTPServer.request_queue_size = 128
    server = ThreadingHTTPServer(("127.0.0.1", args.port), make_handler(fx))
    print(f"LISTENING {server.server_address[1]}", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    sys.exit(main())
