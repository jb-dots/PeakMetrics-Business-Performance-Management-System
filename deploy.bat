@echo off
echo.
echo ========================================
echo  PeakMetrics ^> MonsterASP Deploy
echo ========================================
echo.

REM ── Load secrets ─────────────────────────────────────────────────────────
if not exist deploy.secrets.bat (
    echo  ERROR: deploy.secrets.bat not found.
    echo  Create it from deploy.secrets.example.bat and fill in your credentials.
    pause
    exit /b 1
)
call deploy.secrets.bat

REM ── Deploy via WebDeploy ──────────────────────────────────────────────────
echo Deploying to peakmetrics.runasp.net...
echo.

dotnet publish PeakMetrics.Web.csproj -c Release ^
  /p:WebPublishMethod=MSDeploy ^
  /p:MSDeployServiceURL=%WD_SERVER%:%WD_PORT% ^
  /p:DeployIisAppPath=%WD_SITE% ^
  /p:UserName=%WD_USER% ^
  /p:Password=%WD_PASS% ^
  /p:AllowUntrustedCertificate=True ^
  /p:SkipExtraFilesOnServer=True ^
  --nologo

if %errorlevel% neq 0 (
    echo.
    echo  ERROR: Deploy failed.
    pause
    exit /b 1
)

echo.
echo ========================================
echo  Deploy successful!
echo  https://peakmetrics.runasp.net
echo ========================================
echo.
pause
