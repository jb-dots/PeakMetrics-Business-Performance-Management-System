-- Manual SQL script to add password reset columns to Users table
-- Run this script on the production database if automatic migration fails

USE [db49465];
GO

-- Check if columns already exist before adding them
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetToken')
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetToken] nvarchar(max) NULL;
    PRINT 'Added PasswordResetToken column';
END
ELSE
BEGIN
    PRINT 'PasswordResetToken column already exists';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Users]') AND name = 'PasswordResetTokenExpiry')
BEGIN
    ALTER TABLE [dbo].[Users]
    ADD [PasswordResetTokenExpiry] datetime2(7) NULL;
    PRINT 'Added PasswordResetTokenExpiry column';
END
ELSE
BEGIN
    PRINT 'PasswordResetTokenExpiry column already exists';
END
GO

-- Verify the columns were added
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Users'
AND COLUMN_NAME IN ('PasswordResetToken', 'PasswordResetTokenExpiry');
GO
