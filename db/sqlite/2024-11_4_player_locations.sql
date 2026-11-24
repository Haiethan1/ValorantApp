CREATE TABLE PlayerLocations (
    player_location_index INTEGER PRIMARY KEY AUTOINCREMENT,
    event_index INTEGER NOT NULL,
    player_index INTEGER NOT NULL,
    x INTEGER NOT NULL, -- X-coordinate of the player
    y INTEGER NOT NULL, -- Y-coordinate of the player
	FOREIGN KEY (event_index) REFERENCES MatchEvents(event_index) ON DELETE CASCADE,
    FOREIGN KEY (player_index) REFERENCES ValorantPlayers(player_index),
    UNIQUE(event_index, player_index) -- Ensure a player has only one location per event
);

CREATE INDEX IF NOT EXISTS idx_event_index
ON PlayerLocations (event_index);