CREATE TABLE ValorantChannelMapping (
    val_puuid TEXT NOT NULL,
    disc_channel_id ULONG NOT NULL,
    PRIMARY KEY (val_puuid, disc_channel_id)
);

INSERT INTO ValorantChannelMapping (val_puuid, disc_channel_id)
SELECT val_puuid, 1158083743278432378
FROM ValorantUsers;
