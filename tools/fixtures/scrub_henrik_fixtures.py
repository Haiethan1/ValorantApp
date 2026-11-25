#!/usr/bin/env python3
"""
Scrub raw HenrikDev API responses into committable test fixtures.

Replaces every player identity (puuid, name, tag, "Name#Tag" display names, party ids), match ids and Premier
roster ids/names with consistent fakes across ALL input files, so cross references (kill feed -> player,
mmr-history -> match) still line up. Game asset ids (maps, weapons, seasons, cards) are kept: the bot uses them.

The tracked player (--owner-puuid) becomes "Player01#TAG01" with puuid aaaaaaaa-0000-4000-8000-000000000001.

Ends with a leak scan: if any original identifier survives anywhere in the output, nothing is kept and it exits 1.

Usage (raw files must live OUTSIDE the repo):
  python -I tools/fixtures/scrub_henrik_fixtures.py <raw_dir> <out_dir> --owner-puuid <puuid>
Fetch the raw files with tools/fixtures/fetch_henrik_fixtures.sh. See docs/testing.md "Recorded API fixtures".
"""
import argparse
import json
import os
import re
import shutil
import sys
import tempfile

UUID_RE = re.compile(r"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", re.I)
NAME_KEYS = {"name", "tag"}  # only replaced inside player / account / roster objects (see collect_*)


class Faker:
    def __init__(self, id_base=0):
        self.id_base = id_base
        self.maps = {"puuid": {}, "match": {}, "party": {}, "roster": {}}
        self.prefix = {"puuid": "aaaaaaaa", "match": "bbbbbbbb", "party": "cccccccc", "roster": "dddddddd"}
        self.names = {}       # real name -> fake name
        self.tags = {}        # real (name, tag) -> fake tag
        self.roster_names = {}

    def fake_id(self, kind, real):
        m = self.maps[kind]
        if real not in m:
            m[real] = f"{self.prefix[kind]}-0000-4000-8000-{self.id_base + len(m) + 1:012d}"
        return m[real]

    def add_player(self, puuid, name, tag):
        self.fake_id("puuid", puuid.lower())
        n = len(self.names) + 1
        if name not in self.names:
            self.names[name] = f"Player{n:02d}"
        if (name, tag) not in self.tags:
            self.tags[(name, tag)] = f"TAG{len(self.tags) + 1:02d}"

    def all_ids(self):
        for kind, m in self.maps.items():
            for real, fake in m.items():
                yield kind, real, fake


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def players_of(match):
    p = (match or {}).get("players") or {}
    return p.get("all_players") or []


def collect(docs, faker, owner_puuid, tracked=()):
    """First pass: learn every identity, owner first so they get index 01."""
    owner_name = owner_tag = None
    for doc in docs.values():
        for m in data_list(doc):
            for p in players_of(m):
                if p.get("puuid", "").lower() == owner_puuid:
                    owner_name, owner_tag = p.get("name"), p.get("tag")
    acc = (docs.get("account.json") or {}).get("data") or {}
    if acc.get("puuid", "").lower() == owner_puuid:
        owner_name, owner_tag = acc.get("name"), acc.get("tag")
    if owner_name is None:
        sys.exit(f"Owner puuid {owner_puuid} not found in any match or account file")
    faker.add_player(owner_puuid, owner_name, owner_tag)

    # Extra tracked players get the next fixed indexes (Player02, Player03, ...) so tests can name them.
    for extra in tracked:
        for doc in docs.values():
            hit = next((p for m in data_list(doc) for p in players_of(m) if p.get("puuid", "").lower() == extra), None)
            if hit:
                faker.add_player(extra, hit.get("name"), hit.get("tag"))
                break
        else:
            sys.exit(f"Tracked puuid {extra} not found in any match file")

    for doc in docs.values():
        for m in data_list(doc):
            meta = m.get("metadata") or {}
            if meta.get("matchid"):
                faker.fake_id("match", meta["matchid"].lower())
            for p in players_of(m):
                faker.add_player(p["puuid"].lower(), p.get("name"), p.get("tag"))
                if p.get("party_id"):
                    faker.fake_id("party", p["party_id"].lower())
            for side in ("red", "blue"):
                roster = ((m.get("teams") or {}).get(side) or {}).get("roster") or {}
                if roster.get("id"):
                    faker.fake_id("roster", roster["id"].lower())
                if roster.get("name") and roster["name"] not in faker.roster_names:
                    faker.roster_names[roster["name"]] = f"Team{len(faker.roster_names) + 1:02d}"
        # mmr-history entries reference match ids too
        if isinstance(doc, dict) and isinstance(doc.get("data"), list):
            for e in doc["data"]:
                if isinstance(e, dict) and isinstance(e.get("match_id"), str) and UUID_RE.match(e["match_id"]):
                    faker.fake_id("match", e["match_id"].lower())


def data_list(doc):
    d = doc.get("data") if isinstance(doc, dict) else None
    if isinstance(d, list):
        return [m for m in d if isinstance(m, dict) and "players" in m]
    return []


def scrub_value(v, faker, id_lookup, display_lookup):
    if isinstance(v, str):
        low = v.lower()
        if low in id_lookup:
            return id_lookup[low]
        if v in display_lookup:
            return display_lookup[v]
    return v


def scrub(o, faker, id_lookup, display_lookup, in_identity=False, real_name=None):
    if isinstance(o, dict):
        # Objects that carry a player identity (player rows, account) get name/tag replaced.
        # (mmr-history puts name/tag at the top level without a puuid, so a known player name also counts.)
        identity = ("puuid" in o and ("name" in o or "tag" in o)) or (
            "tag" in o and isinstance(o.get("name"), str) and o["name"] in faker.names)
        name = o.get("name") if identity else None
        out = {}
        for k, v in o.items():
            if identity and k == "name" and isinstance(v, str):
                out[k] = faker.names.get(v, "PlayerX")
            elif identity and k == "tag" and isinstance(v, str):
                out[k] = faker.tags.get((name, v), "TAGX")
            elif k == "roster" and isinstance(v, dict):
                r = dict(v)
                if isinstance(r.get("name"), str):
                    r["name"] = faker.roster_names.get(r["name"], "TeamX")
                if isinstance(r.get("tag"), str):
                    r["tag"] = "TM"
                out[k] = scrub(r, faker, id_lookup, display_lookup)
            elif k.endswith("display_name") and isinstance(v, str):
                out[k] = display_lookup.get(v, "PlayerX#TAGX")
            else:
                out[k] = scrub(v, faker, id_lookup, display_lookup)
        return out
    if isinstance(o, list):
        return [scrub(v, faker, id_lookup, display_lookup) for v in o]
    return scrub_value(o, faker, id_lookup, display_lookup)


def strings(o):
    if isinstance(o, dict):
        for k, v in o.items():
            yield from strings(v)
    elif isinstance(o, list):
        for v in o:
            yield from strings(v)
    elif isinstance(o, str):
        yield o


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("raw_dir")
    ap.add_argument("out_dir")
    ap.add_argument("--owner-puuid", required=True)
    ap.add_argument("--tracked", nargs="*", default=[], help="more puuids to pin as Player02, Player03, ...")
    ap.add_argument("--id-base", type=int, default=0, help="offset for fake ids, so separate fixture sets don't collide")
    args = ap.parse_args()

    repo = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    if os.path.abspath(args.raw_dir).startswith(repo + os.sep):
        sys.exit("Raw files must live outside the repository.")

    docs = {f: load(os.path.join(args.raw_dir, f)) for f in sorted(os.listdir(args.raw_dir)) if f.endswith(".json")}
    faker = Faker(args.id_base)
    owner = args.owner_puuid.lower()
    collect(docs, faker, owner, [t.lower() for t in args.tracked])

    id_lookup = {real: fake for _, real, fake in faker.all_ids()}
    display_lookup = {}
    for (name, tag), fake_tag in faker.tags.items():
        display_lookup[f"{name}#{tag}"] = f"{faker.names[name]}#{fake_tag}"

    scrubbed = {f: scrub(doc, faker, id_lookup, display_lookup) for f, doc in docs.items()}

    # Leak scan: no original identifier may survive as a value or inside a string.
    secrets = set(id_lookup) | set(display_lookup) | {n for n in faker.names if n and len(n) >= 3} | set(faker.roster_names)
    leaks = []
    for f, doc in scrubbed.items():
        for s in strings(doc):
            low = s.lower()
            for secret in secrets:
                if secret.lower() == low or (len(secret) >= 8 and secret.lower() in low):
                    leaks.append((f, secret[:3] + "..."))
                    break
    if leaks:
        print(f"LEAK SCAN FAILED: {len(leaks)} values still contain original identifiers, nothing written.", file=sys.stderr)
        for f, hint in leaks[:20]:
            print(f"  {f}: {hint}", file=sys.stderr)
        sys.exit(1)

    tmp = tempfile.mkdtemp()
    for f, doc in scrubbed.items():
        with open(os.path.join(tmp, f), "w", encoding="utf-8", newline="\n") as out:
            json.dump(doc, out, ensure_ascii=False, separators=(",", ":"))
    os.makedirs(args.out_dir, exist_ok=True)
    for f in scrubbed:
        shutil.move(os.path.join(tmp, f), os.path.join(args.out_dir, f))
    shutil.rmtree(tmp, ignore_errors=True)

    print(f"Scrubbed {len(scrubbed)} files: {len(faker.maps['puuid'])} players, {len(faker.maps['match'])} matches, "
          f"{len(faker.maps['party'])} parties, {len(faker.maps['roster'])} rosters. Leak scan passed.")
    print(f"Owner -> Player01#TAG01, puuid {faker.fake_id('puuid', owner)}")


if __name__ == "__main__":
    main()
