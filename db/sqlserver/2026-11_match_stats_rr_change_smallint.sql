-- lu_match_stats.rr_change was created as TINYINT (0-255) in 2024-11_schema.sql, which can't hold RR losses.
-- Prod was already changed to SMALLINT by hand (confirmed 2026-11-24), so this only brings fresh databases
-- (dev, test containers) in line with prod. Safe to run on prod: it does nothing when the column is already SMALLINT.
USE [Valorant];
GO

IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'lu_match_stats' AND COLUMN_NAME = 'rr_change' AND DATA_TYPE = 'tinyint'
)
BEGIN
    ALTER TABLE lu_match_stats ALTER COLUMN rr_change SMALLINT NULL;
END
GO
