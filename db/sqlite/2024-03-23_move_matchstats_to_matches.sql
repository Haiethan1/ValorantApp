
INSERT INTO Matches (match_id, game_start_patched_utc, map, mode, game_length)
SELECT match_id,
       datetime(game_start_patched, '-5 hours') AS game_start_patched_utc,
	   map, mode, game_length
FROM (
    SELECT match_id,
           game_start_patched,
		   map, mode, game_length,
           ROW_NUMBER() OVER (PARTITION BY match_id ORDER BY game_start_patched) AS rn
    FROM MatchStats
) AS ranked
WHERE rn = 1
AND match_id NOT IN (SELECT match_id FROM Matches);