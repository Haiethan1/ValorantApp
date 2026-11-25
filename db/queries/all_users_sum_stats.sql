SELECT
    u.val_username AS "Username",
    u.val_tagname AS "Tag",
    u.val_affinity AS "Region",
    COUNT(m.match_id) AS "Matches Played",
    SUM(m.game_length) / 60 AS "Total Game Length (minutes)",
    SUM(m.rounds_played) AS "Total Rounds Played",
    SUM(s.rr_change) AS "Total RR Change",
    SUM(s.kills) AS "Total Kills",
    SUM(s.deaths) AS "Total Deaths",
    SUM(s.assists) AS "Total Assists",
    SUM(s.aces) AS "Total Aces",
    SUM(s.c_casts) AS "Total Casts (C)",
    SUM(s.q_casts) AS "Total Casts (Q)",
    SUM(s.e_casts) AS "Total Casts (E)",
    SUM(s.x_casts) AS "Total Casts (X)",
    SUM(s.mvp) AS "Total MVPs"
FROM
    MatchStats s
JOIN
    ValorantUsers u ON s.val_puuid = u.val_puuid
JOIN
    Matches m ON s.match_id = m.match_id
WHERE
    m.game_start_patched_utc >= '2024-03-19 00:00:00'
    AND m.game_start_patched_utc <= datetime('now')
    AND m.mode_id = 'competitive'
GROUP BY
    u.val_username, u.val_tagname, u.val_affinity
ORDER BY
    SUM(m.game_length) DESC;
