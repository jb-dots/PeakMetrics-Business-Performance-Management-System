# Forgot Password Feature - Deployment Instructions

## Issue Summary

The forgot password feature implementation is complete, but deployment failed due to a **database migration issue**. The production database is missing two required columns:
- `PasswordResetToken` (nvarchar(max), nullable)
- `PasswordResetTokenExpiry` (datetime2, nullable)

## Root Cause

The initial migration file (`20260508113244_AddPasswordResetFields.cs`) was attempting to CREATE all tables from scratch instead of just ADDING the two new columns to the existing Users table. This caused the error:

```
Invalid column name 'PasswordResetToken'.
Invalid column name 'PasswordResetTokenExpiry'.
```

## Solution

I've created a new migration file that only adds these two columns:
- `Migrations/20260508120000_AddPasswordResetColumnsToUsers.cs`

## Deployment Options

### Option 1: Automatic Migration (Recommended)

The application is configured to automatically apply migrations on startup (see `Program.cs` lines 60-72). Simply deploy the application and the migration will run automatically:

```bash
.\deploy.bat
```

**Note**: The deployment failed due to network connectivity issues with the MonsterASP server. You may need to:
1. Check your internet connection
2. Verify the deployment server is accessible
3. Check if there are any firewall restrictions
4. Try deploying again later

### Option 2: Manual SQL Script (If Automatic Migration Fails)

If the automatic migration fails or you prefer manual control, run the SQL script directly on the production database:

1. Connect to the production database:
   - Server: `db49465.databaseasp.net,1433`
   - Database: `db49465`
   - User: `db49465`
   - Password: (from your `appsettings.json`)

2. Run the script: `add-password-reset-columns.sql`

3. Verify the columns were added:
   ```sql
   SELECT COLUMN_NAME, DATA_TYPE, IS_NULLABLE
   FROM INFORMATION_SCHEMA.COLUMNS
   WHERE TABLE_NAME = 'Users'
   AND COLUMN_NAME IN ('PasswordResetToken', 'PasswordResetTokenExpiry');
   ```

4. Deploy the application:
   ```bash
   .\deploy.bat
   ```

## What Was Fixed

1. ✅ Deleted the problematic migration file that tried to create all tables
2. ✅ Created a new migration that only adds the two password reset columns
3. ✅ Removed error diagnostics from the ForgotPassword action (anti-enumeration protection)
4. ✅ Created a manual SQL script as a backup deployment option

## Testing After Deployment

Once deployed successfully, test the forgot password flow:

1. Navigate to https://peakmetrics.runasp.net/Home/Login
2. Click "Forgot Password?" link
3. Enter a valid email address (e.g., `admin@peakmetrics.com`)
4. Check the email inbox for the password reset link
5. Click the link and reset the password
6. Verify you can log in with the new password

## Files Modified

- `Migrations/20260508120000_AddPasswordResetColumnsToUsers.cs` - New migration (ADDED)
- `Migrations/20260508113244_AddPasswordResetFields.cs` - Problematic migration (DELETED)
- `Migrations/20260508113244_AddPasswordResetFields.Designer.cs` - Designer file (DELETED)
- `Controllers/AccountController.cs` - Removed error diagnostics
- `add-password-reset-columns.sql` - Manual SQL script (ADDED)

## Next Steps

1. **Retry deployment**: Try running `.\deploy.bat` again
2. **If deployment fails**: Use Option 2 (manual SQL script)
3. **Test the feature**: Follow the testing steps above
4. **Monitor logs**: Check for any errors in the application logs

## Support

If you encounter any issues:
1. Check the application logs for detailed error messages
2. Verify the database columns were added correctly
3. Ensure the email service is configured properly in `appsettings.json`
4. Test with a valid email address that you have access to

---

**Status**: Ready for deployment
**Migration File**: `Migrations/20260508120000_AddPasswordResetColumnsToUsers.cs`
**SQL Script**: `add-password-reset-columns.sql`
