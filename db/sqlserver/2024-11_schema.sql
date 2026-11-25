USE [master];
GO

CREATE DATABASE Valorant;
GO

USE [Valorant];
GO

CREATE TABLE lu_valorant_users (
    val_puuid NVARCHAR(40) NOT NULL,
    val_username NVARCHAR(20) NOT NULL,
    val_tagname NVARCHAR(5) NOT NULL,
    val_affinity NVARCHAR(5) NOT NULL,
    disc_id BIGINT NOT NULL,
    PRIMARY KEY (val_puuid)
);

CREATE TABLE lu_valorant_channel_mappings (
    val_puuid NVARCHAR(40) NOT NULL,
    disc_channel_id BIGINT NOT NULL, 
    PRIMARY KEY (val_puuid, disc_channel_id)
);

CREATE TABLE lu_match_stats (
    match_id NVARCHAR(40) NOT NULL,
    val_puuid NVARCHAR(40) NOT NULL,
    val_character NVARCHAR(20) NOT NULL,
    rr_change TINYINT,
    double_kills TINYINT NOT NULL,
    triple_kills TINYINT NOT NULL,
    quad_kills TINYINT NOT NULL,
    aces TINYINT NOT NULL,
    kills TINYINT NOT NULL,
    deaths TINYINT NOT NULL,
    assists TINYINT NOT NULL,
    bodyshots FLOAT NOT NULL,
    headshots FLOAT NOT NULL,
    score SMALLINT NOT NULL,
    damage SMALLINT NOT NULL,
    c_casts TINYINT NOT NULL,
    q_casts TINYINT NOT NULL,
    e_casts TINYINT NOT NULL,
    x_casts TINYINT NOT NULL,
    damage_to_allies SMALLINT NOT NULL,
    damage_from_allies SMALLINT NOT NULL,
    mvp BIT NOT NULL DEFAULT 0,
    current_tier TINYINT,
    team NVARCHAR(40),
    new_tier TINYINT,
    PRIMARY KEY (match_id, val_puuid)
);

CREATE TABLE lu_matches (
    match_index INT IDENTITY(1,1) PRIMARY KEY,
    match_id NVARCHAR(40) NOT NULL UNIQUE,
    map NVARCHAR(20),
    mode NVARCHAR(20),
    mode_id NVARCHAR(20),
    game_length SMALLINT NOT NULL DEFAULT 0 CHECK (game_length >= 0),
    game_start INT,
    game_start_patched_utc DATETIME2,
    rounds_played TINYINT,
    blue_team_rounds_won TINYINT,
    red_team_rounds_won TINYINT,
    blue_team_average_rank TINYINT,
    red_team_average_rank TINYINT,
    blue_team_win BIT,
    season_id NVARCHAR(40)
);
GO

CREATE TABLE lu_valorant_players (
    player_index INT IDENTITY(1,1) PRIMARY KEY,
    val_puuid NVARCHAR(40) UNIQUE,
    last_access_time_utc DATETIME2 NOT NULL
);

CREATE TABLE lu_match_players (
    match_index INT NOT NULL,
    player_index INT NOT NULL,
    team TINYINT NULL, -- Matches TeamEnum
    agent TINYINT NOT NULL, -- Matches AgentsEnum
    PRIMARY KEY (match_index, player_index),
    FOREIGN KEY (player_index) REFERENCES lu_valorant_players(player_index),
    FOREIGN KEY (match_index) REFERENCES lu_matches(match_index) ON DELETE CASCADE
);
GO

CREATE TABLE lu_match_events (
    event_index INT IDENTITY(1,1) PRIMARY KEY,
    match_index INT NOT NULL, 
    round_number TINYINT NOT NULL,
    event_type TINYINT NOT NULL, -- Matches EventTypeEnum
    event_time_round_ms INT NOT NULL,
    event_time_match_ms INT NULL,
    initiator_player_index INT NULL,
    target_player_index INT NULL,
    FOREIGN KEY (match_index) REFERENCES lu_matches(match_index) ON DELETE CASCADE,
    FOREIGN KEY (initiator_player_index) REFERENCES lu_valorant_players(player_index),
    FOREIGN KEY (target_player_index) REFERENCES lu_valorant_players(player_index),
    UNIQUE (match_index, round_number, event_type, event_time_round_ms, initiator_player_index, target_player_index)
);
CREATE INDEX idx_match_round
ON lu_match_events (match_index, round_number);
GO

CREATE TABLE lu_player_locations (
    player_location_index INT IDENTITY(1,1) PRIMARY KEY,
    event_index INT NOT NULL,
    player_index INT NOT NULL,
    x SMALLINT NOT NULL,
    y SMALLINT NOT NULL,
    view_radians FLOAT NOT NULL,
    FOREIGN KEY (event_index) REFERENCES lu_match_events(event_index) ON DELETE CASCADE,
    FOREIGN KEY (player_index) REFERENCES lu_valorant_players(player_index),
    UNIQUE (event_index, player_index)
);
CREATE INDEX idx_event_index
ON lu_player_locations (event_index);
GO

CREATE TABLE lu_query_log (
    id INT IDENTITY(1,1) PRIMARY KEY,
    query_text NVARCHAR(255) NOT NULL,
    parameters_text NVARCHAR(255),
    execution_time INT,
    retry_count TINYINT,
    success BIT,
    timestamp_utc DATETIME2 DEFAULT GETUTCDATE()
);
GO