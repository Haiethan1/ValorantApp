
CREATE TABLE ValorantPlayers (
    player_index INTEGER PRIMARY KEY AUTOINCREMENT,
    puuid TEXT UNIQUE, -- Riot's puuid
	last_access_time INTEGER NOT NULL -- The last time we tried accessing this row
);

CREATE TABLE MatchPlayers (
    match_index INTEGER NOT NULL, -- match index unique to shamebot
    player_index INTEGER NOT NULL, -- player index unique to shamebot
	team TINYINT NULL, -- Matches TeamEnum
	agent TINYINT NOT NULL, -- Matches AgentsEnum
    PRIMARY KEY (match_index, player_index),
    FOREIGN KEY (player_index) REFERENCES ValorantPlayers(player_index),
    FOREIGN KEY (match_index) REFERENCES Matches(match_index) ON DELETE CASCADE
);

