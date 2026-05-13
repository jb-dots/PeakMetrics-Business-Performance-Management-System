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

REM ── Clean previous publish output ────────────────────────────────────────
if exist bin\Release\net8.0\publish rmdir /s /q bin\Release\net8.0\publish

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

REM ── Wait for IIS to spin up then force-seed sample data ─────────────────────
echo.
echo Waiting 20 seconds for app to start on MonsterASP...
timeout /t 20 /nobreak > nul
echo.
echo Seeding sample data...
curl -s "https://peakmetrics.runasp.net/api/seed"
echo.
echo Seed complete.
echo.
echo ========================================
echo  Deploy complete! Check the site:
echo  https://peakmetrics.runasp.net
echo ========================================
echo.
pause
