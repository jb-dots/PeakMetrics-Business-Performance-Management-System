@REM Copy this file to deploy.secrets.bat and fill in your credentials.
@REM deploy.secrets.bat is gitignored and should NEVER be committed.

REM WebDeploy credentials (from MonsterASP > WebDeploy Access panel)
SET WD_SERVER=your-webdeploy-server
SET WD_PORT=8172
SET WD_SITE=your-site-name
SET WD_USER=your-webdeploy-username
SET WD_PASS=your-webdeploy-password

REM FTP credentials (from MonsterASP > FTP Access panel)
SET FTP_HOST=ftp://your-ftp-host
SET FTP_USER=your-ftp-username
SET FTP_PASS=your-ftp-password
