CREATE TABLE IF NOT EXISTS Matches_new (
	match_index INTEGER PRIMARY KEY AUTOINCREMENT,
    match_id TEXT UNIQUE NOT NULL,
    map TEXT,
    mode TEXT,
    mode_id TEXT,
    game_length INTEGER NOT NULL DEFAULT 0 CHECK(game_length >= 0),
    game_start INTEGER,
    game_start_patched_utc INTEGER,
    rounds_played INTEGER,
    blue_team_rounds_won INTEGER,
    red_team_rounds_won INTEGER,
    blue_team_average_rank INTEGER,
    red_team_average_rank INTEGER,
    blue_team_win BOOLEAN,
    season_id TEXT
);

INSERT INTO Matches_new (
    match_id,
    map,
    mode,
    mode_id,
    game_length,
    game_start,
    game_start_patched_utc,
    rounds_played,
    blue_team_rounds_won,
    red_team_rounds_won,
    blue_team_average_rank,
    red_team_average_rank,
    blue_team_win,
    season_id
)
SELECT 
    match_id,
    map,
    mode,
    mode_id,
    game_length,
    game_start,
    game_start_patched_utc,
    rounds_played,
    blue_team_rounds_won,
    red_team_rounds_won,
    blue_team_average_rank,
    red_team_average_rank,
    blue_team_win,
    season_id
FROM Matches
ORDER BY 
    CASE WHEN game_start IS NULL THEN 0 ELSE 1 END, -- Nulls first
    game_start; -- Then by ascending date

DROP TABLE Matches;
ALTER TABLE Matches_new RENAME TO Matches;