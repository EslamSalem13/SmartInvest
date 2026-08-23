-- ============================================================================
-- SmartInvestDB reset for real-client testing
-- Backup taken before this script: SmartInvestDB_pre_reset_*.bak (see db-backups/)
--
-- Keeps:   __EFMigrationsHistory, AspNetRoles, AspNetRoleClaims (untouched)
--          AspNetUsers: superadmin@gmail.com, admin@gmail.com only
-- Wipes:   every content table to zero rows (list built dynamically below).
--          Governorate/Markaz/Village/MainProgram/SubProgram/ProjectPriority/
--          ProjectStatus are wiped here and re-populated by the app's own
--          idempotent LookupSeeder on next backend startup (Program.cs).
--          ComponentType/ProjectLevel/AccountingUnit/ContractType/Unit/
--          Measurement are wiped and stay empty (not auto-seeded — Settings
--          UI populates them going forward).
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

BEGIN TRANSACTION;

-- ----------------------------------------------------------------------------
-- 1. Delete the 3 leftover test accounts (and their Identity child rows)
-- ----------------------------------------------------------------------------
DECLARE @TestUserIds TABLE (Id NVARCHAR(450));
INSERT INTO @TestUserIds (Id)
SELECT Id FROM AspNetUsers
WHERE Email IN ('perm_test_employee@test.com', 'finding2emp@test.local', 'e2e_employee@test.com');

DELETE FROM AspNetUserClaims WHERE UserId IN (SELECT Id FROM @TestUserIds);
DELETE FROM AspNetUserLogins WHERE UserId IN (SELECT Id FROM @TestUserIds);
DELETE FROM AspNetUserRoles  WHERE UserId IN (SELECT Id FROM @TestUserIds);
DELETE FROM AspNetUserTokens WHERE UserId IN (SELECT Id FROM @TestUserIds);
DELETE FROM AspNetUsers      WHERE Id     IN (SELECT Id FROM @TestUserIds);

DECLARE @TestUserCount INT = (SELECT COUNT(*) FROM @TestUserIds);
PRINT CONCAT('Deleted test accounts: ', @TestUserCount);

-- ----------------------------------------------------------------------------
-- 2. Wipe every content table (everything except migrations history, roles,
--    role claims, and the AspNetUsers/-children tables already handled above).
-- ----------------------------------------------------------------------------
DECLARE @KeepTables TABLE (Name SYSNAME);
INSERT INTO @KeepTables (Name) VALUES
    ('__EFMigrationsHistory'), ('AspNetRoles'), ('AspNetRoleClaims'),
    ('AspNetUsers'), ('AspNetUserClaims'), ('AspNetUserLogins'), ('AspNetUserRoles'), ('AspNetUserTokens');

DECLARE @sql NVARCHAR(MAX) = N'';

-- Disable every FK constraint in the database so table order doesn't matter.
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
    + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
WHERE t.name NOT IN (SELECT Name FROM @KeepTables);
EXEC sp_executesql @sql;

-- Delete every row from every non-kept table.
DECLARE @DeletedTables TABLE (TableName SYSNAME, RowsDeleted INT);
DECLARE @tbl SYSNAME, @delSql NVARCHAR(MAX);
DECLARE tbl_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name FROM sys.tables t WHERE t.name NOT IN (SELECT Name FROM @KeepTables);
OPEN tbl_cursor;
FETCH NEXT FROM tbl_cursor INTO @tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @delSql = N'DELETE FROM ' + QUOTENAME(@tbl) + N';';
    EXEC sp_executesql @delSql;
    INSERT INTO @DeletedTables (TableName, RowsDeleted) VALUES (@tbl, @@ROWCOUNT);
    FETCH NEXT FROM tbl_cursor INTO @tbl;
END
CLOSE tbl_cursor;
DEALLOCATE tbl_cursor;

-- Reseed IDENTITY columns back to 0 (next insert = 1) on tables that have one.
SET @sql = N'';
SELECT @sql = @sql + N'DBCC CHECKIDENT (''' + t.name + N''', RESEED, 0);' + CHAR(10)
FROM sys.tables t
JOIN sys.identity_columns ic ON ic.object_id = t.object_id
WHERE t.name NOT IN (SELECT Name FROM @KeepTables);
EXEC sp_executesql @sql;

-- Re-enable and re-validate every FK constraint.
SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
    + N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
WHERE t.name NOT IN (SELECT Name FROM @KeepTables);
EXEC sp_executesql @sql;

COMMIT TRANSACTION;

PRINT '--- Rows deleted per table ---';
SELECT TableName, RowsDeleted FROM @DeletedTables WHERE RowsDeleted > 0 ORDER BY TableName;

PRINT '--- Remaining login accounts ---';
SELECT u.Email, u.IsActive, r.Name AS Role FROM AspNetUsers u
LEFT JOIN AspNetUserRoles ur ON ur.UserId = u.Id
LEFT JOIN AspNetRoles r ON r.Id = ur.RoleId;

PRINT '--- FK constraints not trusted (should be empty) ---';
SELECT OBJECT_NAME(parent_object_id) AS TableName, name AS ConstraintName
FROM sys.foreign_keys
WHERE is_not_trusted = 1;
