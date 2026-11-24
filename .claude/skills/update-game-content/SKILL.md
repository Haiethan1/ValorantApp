---
name: update-game-content
description: Add or update Valorant game content the bot hardcodes - new agents, maps, game modes, episodes/acts (seasons), rank emojis. Use when Riot releases content, when matches show "Unknown" agent/map/mode, or when the user asks to bring the bot up to date with the game.
---

# Update game content

The bot hardcodes game content in `ValorantApp/Valorant/Enums/` and `ValorantApp/Valorant/Helpers/`.
When HenrikDev returns a value the code doesn't know, it falls back to `Unknown`, so recaps show
"Unknown" and per-agent stats go missing.

## 0. Find what's missing
- Compare against a current source of truth: HenrikDev content endpoint
  (`GET https://api.henrikdev.xyz/valorant/v1/content`, needs the `HenrikToken` header; ask the user to run
  it, don't read the token yourself) or https://valorant-api.com/v1/agents, /v1/maps, /v1/gamemodes (no key).
- Or search prod logs/DB for `Unknown` values.
- Make a list (new agents, maps, modes, latest act dates) and confirm it with the user before editing.

## 1. Agents: `Valorant/Enums/AgentsEnum.cs`
1. **Append** the member at the end of `enum Agents`, after the last agent, with
   `[Description("<exact name as HenrikDev returns it>")]`. **Never reorder or insert in the middle:**
   the numeric value is stored in `MatchPlayers.agent`.
2. `ImageURLFromAgent`: add a full-artwork URL (existing ones use the Valorant Fandom wiki
   `*_Artwork_Full.png`). Ask the user if you can't verify a URL.
3. `Id` (Discord emoji): the user must upload the agent icon as a custom emoji to the emoji server
   (guild `1251242809684529286`) and give you the `<:name:id>` string. Until then, leave it out;
   the fallback shows the agent's name.
4. Optional: add `Images/Agents/<Name>_icon.webp` plus a matching `<Content Include=...>` /
   `<None Remove=...>` pair in `ValorantApp.csproj` to keep the folder complete (currently unused at runtime).

## 2. Maps: `Valorant/Enums/MapsEnum.cs`
1. Add the member in the right `#region` (standard maps vs Team Deathmatch) with `[Description]`.
   Maps are stored as text, but still don't rename existing members.
2. Add the case to **all three** switches: `MapFromString` (string → enum, exact HenrikDev name),
   `StringFromMap`, and `ImageUrlFromMap`.
3. Optional: `Images/Maps/<Name>.webp` + csproj entries.

## 3. Game modes: `Valorant/Enums/ModesEnum.cs`
1. Add the member before `Unknown` with `[Description]`.
2. `ModeFromString` matches on `str.ToLower().Replace(" ", "")`, so add the lower-case, no-space form.
3. `StringFromMode`: this string is sent to HenrikDev as the `mode=` query value (lower-cased in
   `HenrikApi.Match`). Use HenrikDev's mode id (e.g. `swiftplay`, `teamdeathmatch`).
4. Check `MatchStatsExtension`, `MatchesExtension`, and `BaseValorantProgram` for logic that special-cases
   modes (Competitive/Deathmatch). Decide with the user whether the new mode gets recaps/stats.

## 4. Episodes / acts: `Valorant/Helpers/EpisodeActInfos.cs`
- Add entries to `Mapping` with correct start/end UTC dates. The last real act's end date should be
  its actual end, not a far-future placeholder. Keep keys consistent (`E<episode>A<act>`).
- Since 2025 Riot names seasons by year (e.g. "V25 Act 1"); the code currently models these as
  episode 25. Keep that convention unless the user decides otherwise. **Known bug:** existing
  `E25A2`/`E25A3` have `Act = 1` and 2030 end dates. Fix those when updating.

## 5. Ranks: `Valorant/Enums/RankEmojisEnum.cs`
Only if Riot changes the rank ladder. Rank tiers are stored as `TINYINT` (`current_tier`, `new_tier`).

## 6. Verify
1. `dotnet build` and `dotnet test` from the repo root.
2. If a fixture match JSON for the new content is available, add a test in `ValorantAppTests/`.
3. Have the user check on the **dev** bot (debugger attached): `/mmr` or wait for a recap of a match
   using the new agent/map/mode.
4. Commit as `VAL-<issue>: Add <content>`.
