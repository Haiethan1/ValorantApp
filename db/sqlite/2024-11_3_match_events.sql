CREATE TABLE MatchEvents (
    event_index INTEGER PRIMARY KEY AUTOINCREMENT,
    match_index INTEGER NOT NULL,
    round_number TINYINT NOT NULL,
    event_type TINYINT NOT NULL, -- Type of event; EventTypeEnum in valorant/enums
    event_time_round INTEGER NOT NULL, -- Time in ms in round
	event_time_match INTEGER NULL, -- Time in ms in match
    actor_player_index INTEGER NULL,
    target_player_index INTEGER NULL,
	FOREIGN KEY (match_index) REFERENCES Matches(match_index) ON DELETE CASCADE,
    FOREIGN KEY (actor_player_index) REFERENCES ValorantPlayers(player_index),
    FOREIGN KEY (target_player_index) REFERENCES ValorantPlayers(player_index),
	UNIQUE(match_index, round_number, event_type, event_time_round)
);

CREATE INDEX IF NOT EXISTS idx_match_round
ON MatchEvents (match_index, round_number);