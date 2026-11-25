-- lu_match_stats.rr_change was created as TINYINT (0-255) in 2024-11_schema.sql, which can't hold RR losses.
-- Dev and prod are already SMALLINT (confirmed 2026-11-24); the change was applied with a script that never made
-- it into db/. This brings fresh databases (test containers, rebuilds) in line. It does nothing when the column is
-- already SMALLINT, so it's safe to run anywhere.
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
