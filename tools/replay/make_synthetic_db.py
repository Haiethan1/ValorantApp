#!/usr/bin/env python3
"""
Deterministic synthetic database generator for ValorantApp replays (tools/replay/replay.py).
Generates realistic fake user, channel mapping, match, and match stat records over 90 days
leading up to --now.

Usage:
  python -I tools/replay/make_synthetic_db.py <db_path> --now 2026-01-15T11:30:00Z [--seed 1]
"""
import argparse
from datetime import datetime, timedelta, timezone
import os
import random
import sqlite3
import sys
import uuid

MAPS = [
    "Ascent", "Split", "Fracture", "Bind", "Breeze", "Lotus",
    "Pearl", "Icebox", "Haven", "Sunset", "Abyss", "Corrode"
]

AGENTS = [
    "Brimstone", "Viper", "Omen", "Killjoy", "Cypher", "Sova",
    "Sage", "Phoenix", "Jett", "Reyna", "Raze", "Breach",
    "Skye", "Yoru", "Astra", "KAY/O", "Chamber", "Neon",
    "Fade", "Harbor", "Gekko", "Deadlock", "Iso", "Clove",
    "Vyse", "Tejo", "Waylay", "Veto"
]


def to_eastern(dt_utc):
    """
    Converts a UTC datetime to US Eastern time without external packages.
    US DST begins at 2:00 AM local (07:00 UTC) on the 2nd Sunday in March,
    and ends at 2:00 AM local (06:00 UTC) on the 1st Sunday in November.
    """
    year = dt_utc.year
    mar1 = datetime(year, 3, 1, tzinfo=timezone.utc)
    first_sun_mar = 1 + (6 - mar1.weekday()) % 7
    dst_start = datetime(year, 3, first_sun_mar + 7, 7, 0, 0, tzinfo=timezone.utc)

    nov1 = datetime(year, 11, 1, tzinfo=timezone.utc)
    first_sun_nov = 1 + (6 - nov1.weekday()) % 7
    dst_end = datetime(year, 11, first_sun_nov, 6, 0, 0, tzinfo=timezone.utc)

    if dst_start <= dt_utc < dst_end:
        offset = timedelta(hours=-4)
    else:
        offset = timedelta(hours=-5)
    return dt_utc + offset


def check_and_insert(con, table_name, rows):
    """
    Inserts rows into an existing table, strictly enforcing NOT NULL constraints.
    Fails loudly if any NOT NULL column without a default is unpopulated.
    Inserts only columns that actually exist in the table schema.
    """
    if not rows:
        return

    cols_info = con.execute(f"PRAGMA table_info({table_name})").fetchall()
    if not cols_info:
        sys.exit(f"Error: Table '{table_name}' does not exist or has no columns.")

    table_cols = {col[1]: col for col in cols_info}

    # Validate NOT NULL columns without defaults
    for cid, name, col_type, notnull, dflt_value, pk in cols_info:
        # Skip INTEGER PRIMARY KEY autoincrement columns (like match_index in Matches)
        if pk and "INT" in col_type.upper():
            continue
        if notnull and dflt_value is None:
            for idx, r in enumerate(rows):
                if name not in r or r[name] is None:
                    sys.exit(
                        f"Error: Column '{name}' in table '{table_name}' is NOT NULL with no default, "
                        f"but was not populated in row {idx}."
                    )

    # Filter to columns that exist in the table schema
    first_row = rows[0]
    insert_cols = [c for c in first_row.keys() if c in table_cols]
    placeholders = ", ".join(["?"] * len(insert_cols))
    sql = f"INSERT INTO {table_name} ({', '.join(insert_cols)}) VALUES ({placeholders})"

    data = [[r.get(c) for c in insert_cols] for r in rows]
    con.executemany(sql, data)


def parse_args():
    parser = argparse.ArgumentParser(
        description="Deterministic synthetic database generator for ValorantApp replays."
    )
    parser.add_argument("db_path", help="Path to existing SQLite database file")
    parser.add_argument("--now", required=True, help="ISO-8601 UTC timestamp, e.g. 2026-01-15T11:30:00Z")
    parser.add_argument("--seed", type=int, default=1, help="Deterministic random seed (default: 1)")
    return parser.parse_args()


def main():
    args = parse_args()
    if not os.path.isfile(args.db_path):
        sys.exit(f"Error: Database file does not exist: {args.db_path}")

    now_dt = datetime.fromisoformat(args.now.replace("Z", "+00:00")).astimezone(timezone.utc)
    rng = random.Random(args.seed)

    con = sqlite3.connect(args.db_path)
    existing_tables = {
        row[0]
        for row in con.execute("SELECT name FROM sqlite_master WHERE type='table'").fetchall()
    }

    required_tables = ["ValorantUsers", "Matches", "MatchStats"]
    for t in required_tables:
        if t not in existing_tables:
            sys.exit(f"Error: Table '{t}' does not exist in {args.db_path}. Database tables must already exist.")

    channel_table = "ValorantChannelMapping" if "ValorantChannelMapping" in existing_tables else "ValorantChannelMappings"
    if channel_table not in existing_tables:
        sys.exit(f"Error: Channel mapping table does not exist in {args.db_path}.")

    # Clear existing data in target tables for a clean synthetic DB
    con.execute(f"DELETE FROM {channel_table}")
    con.execute("DELETE FROM ValorantUsers")
    con.execute("DELETE FROM MatchStats")
    con.execute("DELETE FROM Matches")

    # 1. Generate 30 Users
    # SynthPlayer01..30, S01..S30, eeeeeeee-0000-4000-8000-0000000000NN, affinity "na", 900000000000000000 + n
    users = []
    user_channels = []
    for n in range(1, 31):
        puuid = f"eeeeeeee-0000-4000-8000-{n:012d}"
        username = f"SynthPlayer{n:02d}"
        tagname = f"S{n:02d}"
        affinity = "na"
        disc_id = 900000000000000000 + n
        users.append({
            "val_username": username,
            "val_tagname": tagname,
            "val_affinity": affinity,
            "val_puuid": puuid,
            "disc_id": disc_id,
        })

        # 3 channels: 910000000000000001..3
        # Distribution: most users in one channel, ~5 users in two channels, 1 user in no channel.
        if n == 30:
            assigned_channels = []  # 1 user in no channel
        elif n == 25:
            assigned_channels = [910000000000000001, 910000000000000002]
        elif n == 26:
            assigned_channels = [910000000000000001, 910000000000000002]
        elif n == 27:
            assigned_channels = [910000000000000001, 910000000000000003]
        elif n == 28:
            assigned_channels = [910000000000000001, 910000000000000003]
        elif n == 29:
            assigned_channels = [910000000000000002, 910000000000000003]
        elif 1 <= n <= 14:
            assigned_channels = [910000000000000001]
        elif 15 <= n <= 19:
            assigned_channels = [910000000000000002]
        else:  # 20 <= n <= 24
            assigned_channels = [910000000000000003]

        for ch in assigned_channels:
            user_channels.append({
                "val_puuid": puuid,
                "disc_channel_id": ch,
            })

    # Track running MMR and tier for each player (tiers 3..27)
    user_state = {}
    for u in users:
        p = u["val_puuid"]
        user_state[p] = {
            "tier": rng.randint(8, 20),
            "rr": rng.randint(20, 80),
        }

    # Matches and MatchStats rows to insert
    match_rows = []
    match_stats_rows = []

    def add_match(party, mode, dt_utc, force_stats=None):
        """Creates a single Matches row and associated MatchStats rows for party members."""
        match_id = str(uuid.UUID(int=rng.getrandbits(128)))
        season_id = "d816f426-48ea-f052-117f-9697a155b319"
        map_name = rng.choice(MAPS)
        game_start = int(dt_utc.timestamp())
        game_start_patched_utc = dt_utc.strftime("%Y-%m-%d %H:%M:%S")
        game_start_patched = to_eastern(dt_utc).strftime("%Y-%m-%d %H:%M:%S")

        if mode == "Deathmatch":
            mode_id = "deathmatch"
            rounds_played = 1
            blue_team_rounds_won = 0
            red_team_rounds_won = 0
            blue_team_win = 0
            game_length = rng.randint(360, 600)
        elif mode == "SwiftPlay":
            mode_id = "swiftplay"
            blue_wins = rng.random() < 0.5
            if blue_wins:
                blue_team_rounds_won = 5
                red_team_rounds_won = rng.randint(0, 4)
                blue_team_win = 1
            else:
                red_team_rounds_won = 5
                blue_team_rounds_won = rng.randint(0, 4)
                blue_team_win = 0
            rounds_played = blue_team_rounds_won + red_team_rounds_won
            game_length = rounds_played * rng.randint(75, 95)
        else:  # Competitive or Unrated
            mode_id = mode.lower()
            blue_wins = rng.random() < 0.5
            overtime = rng.random() < 0.1
            if overtime:
                rounds_played = rng.choice([24, 26])
                winner_rounds = (rounds_played // 2) + 1
                loser_rounds = rounds_played - winner_rounds
            else:
                winner_rounds = 13
                loser_rounds = rng.randint(2, 11)
                rounds_played = winner_rounds + loser_rounds

            if blue_wins:
                blue_team_rounds_won = winner_rounds
                red_team_rounds_won = loser_rounds
                blue_team_win = 1
            else:
                red_team_rounds_won = winner_rounds
                blue_team_rounds_won = loser_rounds
                blue_team_win = 0
            game_length = rounds_played * rng.randint(85, 115)

        avg_tier = int(round(sum(user_state[p]["tier"] for p in party) / len(party)))
        blue_team_average_rank = avg_tier
        red_team_average_rank = max(3, min(27, avg_tier + rng.choice([-1, 0, 1])))

        match_rows.append({
            "match_id": match_id,
            "map": map_name,
            "mode": mode,
            "mode_id": mode_id,
            "game_length": game_length,
            "game_start": game_start,
            "game_start_patched_utc": game_start_patched_utc,
            "rounds_played": rounds_played,
            "blue_team_rounds_won": blue_team_rounds_won,
            "red_team_rounds_won": red_team_rounds_won,
            "blue_team_average_rank": blue_team_average_rank,
            "red_team_average_rank": red_team_average_rank,
            "blue_team_win": blue_team_win,
            "season_id": season_id,
        })

        # Generate player stats for each player in party
        party_stats = []
        for p in party:
            character = rng.choice(AGENTS)
            team = "Blue"

            override = force_stats.get(p) if force_stats else None

            if override and "kills" in override:
                kills = override["kills"]
            elif mode == "Deathmatch":
                kills = rng.randint(15, 38)
            else:
                base = rounds_played * rng.uniform(0.5, 1.0)
                kills = max(0, min(rounds_played * 2, int(round(base))))

            if mode == "Deathmatch":
                deaths = rng.randint(15, 30)
                assists = 0
            else:
                deaths = max(1, min(rounds_played, int(round(rounds_played * rng.uniform(0.5, 0.9)))))
                assists = rng.randint(1, max(2, rounds_played // 2))

            if kills == 0:
                double_kills = triple_kills = quad_kills = aces = 0
                knife_kills = knife_deaths = 0
                damage = rng.randint(200, 450)
                score = int(damage * rng.uniform(1.1, 1.3))
                headshot = 0.0
                bodyshot = round(rng.uniform(40.0, 60.0), 1)
            elif kills >= 30:
                double_kills = rng.randint(3, 5)
                triple_kills = rng.randint(2, 4)
                quad_kills = rng.randint(1, 2)
                aces = rng.randint(0, 1)
                knife_kills = 1 if rng.random() < 0.1 else 0
                knife_deaths = 0
                damage = int(kills * rng.randint(135, 165) + assists * rng.randint(35, 55))
                score = int(damage * rng.uniform(1.4, 1.7))
                headshot = round(rng.uniform(22.0, 36.0), 1)
                bodyshot = round(rng.uniform(55.0, 70.0), 1)
                if headshot + bodyshot > 96.0:
                    bodyshot = round(96.0 - headshot, 1)
            else:
                double_kills = min(kills // 2, rng.randint(0, max(1, kills // 4)))
                triple_kills = min((kills - 2 * double_kills) // 3, rng.randint(0, 2)) if kills >= 6 else 0
                quad_kills = 1 if (kills >= 10 and rng.random() < 0.15) else 0
                aces = 1 if (kills >= 15 and quad_kills == 0 and rng.random() < 0.05) else 0
                knife_kills = 1 if rng.random() < 0.04 else 0
                knife_deaths = 1 if rng.random() < 0.03 else 0
                damage = int(kills * rng.randint(120, 160) + assists * rng.randint(30, 50) + rounds_played * rng.randint(10, 30))
                score = int(damage * rng.uniform(1.2, 1.5))
                headshot = round(rng.uniform(12.0, 32.0), 1)
                bodyshot = round(rng.uniform(55.0, 75.0), 1)
                if headshot + bodyshot > 96.0:
                    bodyshot = round(96.0 - headshot, 1)

            if mode == "Deathmatch":
                c_casts = q_casts = e_casts = x_casts = 0
            else:
                c_casts = rng.randint(0, min(8, rounds_played))
                q_casts = rng.randint(0, min(8, rounds_played))
                e_casts = rng.randint(1, min(14, rounds_played * 2))
                x_casts = rng.randint(0, min(3, rounds_played // 5))

            damage_to_allies = rng.randint(0, 35) if rng.random() < 0.15 else 0
            damage_from_allies = rng.randint(0, 35) if rng.random() < 0.15 else 0

            current_tier = user_state[p]["tier"]
            if mode == "Competitive":
                if blue_team_win == 1:
                    rr_change = rng.randint(14, 28)
                else:
                    rr_change = -rng.randint(12, 24)

                user_state[p]["rr"] += rr_change
                if user_state[p]["rr"] >= 100:
                    user_state[p]["rr"] -= 100
                    new_tier = min(27, current_tier + 1)
                elif user_state[p]["rr"] < 0:
                    user_state[p]["rr"] += 100
                    new_tier = max(3, current_tier - 1)
                else:
                    new_tier = current_tier
                user_state[p]["tier"] = new_tier
            else:
                rr_change = 0
                new_tier = current_tier

            party_stats.append({
                "match_id": match_id,
                "val_puuid": p,
                "map": map_name,
                "mode": mode,
                "rounds": rounds_played,
                "character": character,
                "rr_change": rr_change,
                "double_kills": double_kills,
                "triple_kills": triple_kills,
                "quad_kills": quad_kills,
                "aces": aces,
                "kills": kills,
                "knife_kills": knife_kills,
                "deaths": deaths,
                "knife_deaths": knife_deaths,
                "assists": assists,
                "bodyshots": bodyshot,
                "headshots": headshot,
                "score": score,
                "damage": damage,
                "c_casts": c_casts,
                "q_casts": q_casts,
                "e_casts": e_casts,
                "x_casts": x_casts,
                "damage_to_allies": damage_to_allies,
                "damage_from_allies": damage_from_allies,
                "game_length": game_length,
                "game_start_patched": game_start_patched,
                "current_tier": current_tier,
                "team": team,
                "new_tier": new_tier,
            })

        # Exactly one player per match has MVP=1 (highest score in match)
        highest_score = max(ps["score"] for ps in party_stats)
        mvp_awarded = False
        for ps in party_stats:
            if ps["score"] == highest_score and not mvp_awarded:
                ps["mvp"] = 1
                mvp_awarded = True
            else:
                ps["mvp"] = 0

        match_stats_rows.extend(party_stats)

    # 2. Match history over 90 days before --now
    # Requirements:
    # - User 1 (SynthPlayer01): exactly 0 matches in DB (no matches at all).
    # - User 2 (SynthPlayer02): exactly 1 match in DB.
    # - Users 3..30: regular players, ~60% of days active, 0-4 matches/day.
    # - "Yesterday" (UTC day before --now): >= 20 users across all 3 channels play competitive matches,
    #   including edge cases: 0-kill game and 30+ kill game.

    puuid_user1 = users[0]["val_puuid"]
    puuid_user2 = users[1]["val_puuid"]
    regular_puuids = [u["val_puuid"] for u in users[2:]]

    # Generate days 90 down to 2 (historical days)
    for day_offset in range(90, 1, -1):
        day_date = (now_dt - timedelta(days=day_offset)).date()

        # Day 45: give User 2 their single match
        if day_offset == 45:
            match_time = datetime(day_date.year, day_date.month, day_date.day, 16, 0, 0, tzinfo=timezone.utc)
            add_match([puuid_user2, regular_puuids[0]], "Competitive", match_time)

        # Decide which regular users play today (~60% chance per user)
        active_today = [p for p in regular_puuids if rng.random() < 0.60]
        if not active_today:
            continue

        rng.shuffle(active_today)
        # Partition active users into parties of 2 to 5 (or solo 1)
        parties = []
        idx = 0
        while idx < len(active_today):
            rem = len(active_today) - idx
            if rem >= 5:
                size = rng.randint(2, 5)
            elif rem >= 2:
                size = rng.randint(2, rem)
            else:
                size = 1
            parties.append(active_today[idx:idx + size])
            idx += size

        # Schedule matches for these parties across the day
        hour_base = 14
        minute_base = 0
        for party in parties:
            # Mode mix: ~70% Competitive, rest Unrated / SwiftPlay / Deathmatch
            r = rng.random()
            if r < 0.70:
                mode = "Competitive"
            elif r < 0.80:
                mode = "Unrated"
            elif r < 0.90:
                mode = "SwiftPlay"
            else:
                mode = "Deathmatch"

            dt_match = datetime(day_date.year, day_date.month, day_date.day,
                                hour_base, minute_base, rng.randint(0, 59), tzinfo=timezone.utc)
            add_match(party, mode, dt_match)

            minute_base += 40
            if minute_base >= 60:
                hour_base += minute_base // 60
                minute_base %= 60
                if hour_base >= 23:
                    hour_base = 22

    # 3. Generate "Yesterday" (day_offset == 1)
    # The daily report window for --now at 11:30 UTC is:
    # from yesterday 11:00:01 to today 11:00:00 UTC.
    # We guarantee >= 20 users across all 3 channels have competitive matches in this window.
    yesterday_date = (now_dt - timedelta(days=1)).date()

    # Form parties covering channels 1, 2, 3 and dual-channel users:
    # Users 3..14 (Ch1: 12 users)
    # Users 15..19 (Ch2: 5 users)
    # Users 20..24 (Ch3: 5 users)
    # Users 25..29 (Dual: 5 users)
    # Total: 27 users playing on yesterday!
    yesterday_parties = [
        ([users[2]["val_puuid"], users[3]["val_puuid"], users[4]["val_puuid"], users[5]["val_puuid"]], 14, 0),
        ([users[6]["val_puuid"], users[7]["val_puuid"], users[8]["val_puuid"], users[9]["val_puuid"]], 15, 30),
        ([users[10]["val_puuid"], users[11]["val_puuid"], users[12]["val_puuid"], users[13]["val_puuid"]], 17, 0),
        ([users[14]["val_puuid"], users[15]["val_puuid"], users[16]["val_puuid"], users[17]["val_puuid"], users[18]["val_puuid"]], 18, 30),
        ([users[19]["val_puuid"], users[20]["val_puuid"], users[21]["val_puuid"], users[22]["val_puuid"], users[23]["val_puuid"]], 20, 0),
        ([users[24]["val_puuid"], users[25]["val_puuid"], users[26]["val_puuid"], users[27]["val_puuid"], users[28]["val_puuid"]], 21, 30),
    ]

    p_zero_kill = users[2]["val_puuid"]    # SynthPlayer03 gets 0-kill game
    p_high_kill = users[6]["val_puuid"]    # SynthPlayer07 gets 34-kill game

    for party, hr, mn in yesterday_parties:
        dt_match = datetime(yesterday_date.year, yesterday_date.month, yesterday_date.day,
                            hr, mn, rng.randint(0, 59), tzinfo=timezone.utc)
        force_stats = {}
        if p_zero_kill in party:
            force_stats[p_zero_kill] = {"kills": 0}
        if p_high_kill in party:
            force_stats[p_high_kill] = {"kills": 34}

        add_match(party, "Competitive", dt_match, force_stats=force_stats)

    # 4. Insert all rows into database
    check_and_insert(con, "ValorantUsers", users)
    check_and_insert(con, channel_table, user_channels)
    check_and_insert(con, "Matches", match_rows)
    check_and_insert(con, "MatchStats", match_stats_rows)

    con.commit()
    con.close()

    print(
        f"Successfully generated synthetic DB at '{args.db_path}':\n"
        f"  - Users: {len(users)}\n"
        f"  - Channel mappings: {len(user_channels)}\n"
        f"  - Matches: {len(match_rows)}\n"
        f"  - MatchStats: {len(match_stats_rows)}\n"
        f"  - Reference time: {now_dt.isoformat()}\n"
        f"  - Seed: {args.seed}"
    )


if __name__ == "__main__":
    main()
