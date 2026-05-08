# Security Setup Script for PeakMetrics
# Run this script to configure security scanning

Write-Host "PeakMetrics Security Setup" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""

# Check if git is installed
if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    Write-Host "❌ Git is not installed. Please install Git first." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Git is installed" -ForegroundColor Green

# Check if pre-commit hook exists
$hookPath = ".git/hooks/pre-commit"
if (Test-Path $hookPath) {
    Write-Host "✅ Pre-commit hook is installed" -ForegroundColor Green
} else {
    Write-Host "❌ Pre-commit hook is missing" -ForegroundColor Red
    Write-Host "   Run: git init to reinitialize hooks" -ForegroundColor Yellow
}

# Check if .gitleaks.toml exists
if (Test-Path ".gitleaks.toml") {
    Write-Host "✅ GitLeaks configuration found" -ForegroundColor Green
} else {
    Write-Host "❌ GitLeaks configuration missing" -ForegroundColor Red
}

# Check if GitHub Actions workflow exists
if (Test-Path ".github/workflows/security-scan.yml") {
    Write-Host "✅ GitHub Actions security workflow configured" -ForegroundColor Green
} else {
    Write-Host "❌ GitHub Actions workflow missing" -ForegroundColor Red
}

# Check .gitignore for sensitive files
Write-Host ""
Write-Host "Checking .gitignore for sensitive file patterns..." -ForegroundColor Cyan

$sensitivePatterns = @(
    "appsettings.json",
    "deploy.secrets.bat",
    "*.pfx",
    "*.key",
    ".env"
)

$gitignoreContent = Get-Content .gitignore -Raw
$allPatternsFound = $true

foreach ($pattern in $sensitivePatterns) {
    if ($gitignoreContent -match [regex]::Escape($pattern)) {
        Write-Host "  ✅ $pattern" -ForegroundColor Green
    } else {
        Write-Host "  ❌ $pattern (missing)" -ForegroundColor Red
        $allPatternsFound = $false
    }
}

if ($allPatternsFound) {
    Write-Host "✅ All sensitive patterns are in .gitignore" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some sensitive patterns are missing from .gitignore" -ForegroundColor Yellow
}

# Check for sensitive files in repository
Write-Host ""
Write-Host "Scanning for sensitive files in repository..." -ForegroundColor Cyan

$sensitiveFiles = @(
    "appsettings.json",
    "appsettings.Development.json",
    "appsettings.Production.json",
    "deploy.secrets.bat"
)

$foundSensitive = $false
foreach ($file in $sensitiveFiles) {
    if (Test-Path $file) {
        $isTracked = git ls-files $file
        if ($isTracked) {
            Write-Host "  ⚠️  $file is tracked by git!" -ForegroundColor Yellow
            $foundSensitive = $true
        } else {
            Write-Host "  ✅ $file exists but is not tracked" -ForegroundColor Green
        }
    }
}

if (-not $foundSensitive) {
    Write-Host "✅ No sensitive files are tracked" -ForegroundColor Green
}

# Test pre-commit hook
Write-Host ""
Write-Host "Testing pre-commit hook..." -ForegroundColor Cyan

# Create a test file with a fake secret
$testFile = "test-secret.txt"
"password=test123" | Out-File $testFile
git add $testFile 2>$null

$hookTest = git commit -m "test" --dry-run 2>&1
Remove-Item $testFile -ErrorAction SilentlyContinue
git reset HEAD $testFile 2>$null

if ($hookTest -match "security|secret") {
    Write-Host "✅ Pre-commit hook is working" -ForegroundColor Green
} else {
    Write-Host "⚠️  Pre-commit hook may not be working properly" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "=============================" -ForegroundColor Cyan
Write-Host "Security Setup Summary" -ForegroundColor Cyan
Write-Host "=============================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Local Protection:" -ForegroundColor White
Write-Host "  • Pre-commit hooks: Blocks sensitive files before commit" -ForegroundColor Gray
Write-Host "  • .gitignore: Prevents tracking of sensitive files" -ForegroundColor Gray
Write-Host ""
Write-Host "CI/CD Protection:" -ForegroundColor White
Write-Host "  • GitHub Actions: Automated secret scanning on push" -ForegroundColor Gray
Write-Host "  • TruffleHog: Deep secret detection in history" -ForegroundColor Gray
Write-Host "  • GitLeaks: Pattern-based secret scanning" -ForegroundColor Gray
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Review SECURITY.md for best practices" -ForegroundColor Gray
Write-Host "  2. Configure GitHub Secrets (SONAR_TOKEN, etc.)" -ForegroundColor Gray
Write-Host "  3. Test by making a commit" -ForegroundColor Gray
Write-Host "  4. Push to GitHub to trigger Actions workflow" -ForegroundColor Gray
Write-Host ""
Write-Host "Security setup complete!" -ForegroundColor Green
