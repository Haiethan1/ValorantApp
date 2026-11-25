#!/usr/bin/env bash
# Fetches raw HenrikDev responses for building test fixtures. Saves to ~/henrik-fixtures-raw (outside the repo).
# Run from Git Bash:  bash tools/fixtures/fetch_henrik_fixtures.sh <name> <tag> <puuid> [affinity]
# It asks for the API key (input hidden) so the key is never written to a file or shell history.
# Then scrub with tools/fixtures/scrub_henrik_fixtures.py. Never commit the raw files.
set -u
[ $# -lt 3 ] && { echo "usage: $0 <name> <tag> <puuid> [affinity=na]"; exit 2; }
NAME=$1; TAG=$2; P=$3; AFF=${4:-na}

read -rsp "HenrikDev API key: " HENRIK_KEY; echo
[ -z "$HENRIK_KEY" ] && { echo "No key entered"; exit 1; }

OUT="$HOME/henrik-fixtures-raw"
mkdir -p "$OUT" && cd "$OUT" || exit 1

B=https://api.henrikdev.xyz/valorant

get() {
  code=$(curl -s -o "$1.json" -w '%{http_code}' -H "Authorization: $HENRIK_KEY" "$B/$2")
  echo "$code  $1"
  sleep 2
}

# The exact calls the bot makes
get account            "v1/account/$NAME/$TAG"
get mmr                "v2/by-puuid/mmr/$AFF/$P"
get mmr_history        "v1/by-puuid/mmr-history/$AFF/$P"
get matches_latest     "v3/by-puuid/matches/$AFF/$P?size=1"
get matches_latest10   "v3/by-puuid/matches/$AFF/$P?size=10"

# One match per game mode (empty "data" just means no recent game in that mode)
for m in competitive unrated swiftplay deathmatch teamdeathmatch spikerush escalation premier custom replication snowball newmap; do
  get "matches_$m" "v3/by-puuid/matches/$AFF/$P?mode=$m&size=1"
done

# Error-response shape
get account_notfound   "v1/account/zzznotarealuser999/zzz1"

echo
echo "Saved to: $OUT"
ls -la "$OUT"
