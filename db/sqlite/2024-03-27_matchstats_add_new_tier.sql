alter TABLE MatchStats add COLUMN new_tier TINYINT;

UPDATE MatchStats SET new_tier = current_tier;