# Security Quick Start Guide

## ✅ What's Already Set Up

Your repository now has **3 layers of security protection**:

### 1. Local Protection (Pre-Commit Hooks)
- Automatically runs before every commit
- Blocks sensitive files from being committed
- Detects personal emails in migrations
- Warns about hardcoded secrets

### 2. GitHub Actions (Automated CI/CD)
- Runs on every push and pull request
- **TruffleHog**: Scans entire git history for secrets
- **GitLeaks**: Pattern-based secret detection
- **Dependency Scanner**: Checks for vulnerable packages

### 3. Custom Configuration
- `.gitleaks.toml`: Custom rules for .NET projects
- `.gitignore`: Enhanced with security patterns
- `SECURITY.md`: Complete security policy

## 🚀 Quick Commands

### Test Security Setup
```powershell
.\setup-security.ps1
```

### Commit with Security Checks
```bash
git add .
git commit -m "your message"
# Pre-commit hook runs automatically
```

### Bypass Hook (Emergency Only)
```bash
git commit --no-verify -m "your message"
```

### Check for Secrets Manually
```bash
# If you have GitLeaks installed locally
gitleaks detect --source . --verbose
```

## 📋 Pre-Push Checklist

Before pushing to GitHub:

- [ ] Run `.\setup-security.ps1` to verify setup
- [ ] Review `git diff` for sensitive data
- [ ] Ensure no personal emails in migrations
- [ ] Check that sensitive files are in `.gitignore`
- [ ] Verify environment variables are used for secrets

## 🔧 GitHub Setup (One-Time)

### Enable GitHub Secret Scanning
1. Go to **Settings** → **Code security and analysis**
2. Enable **Secret scanning**
3. Enable **Push protection** (blocks pushes with secrets)

### Configure GitHub Secrets (Optional)
For advanced features, add these secrets:

1. Go to **Settings** → **Secrets and variables** → **Actions**
2. Click **New repository secret**
3. Add:
   - `SONAR_TOKEN` (for SonarCloud analysis)
   - `GITLEAKS_LICENSE` (if you have GitLeaks Pro)

## 🚨 What Gets Blocked

### Sensitive Files
- `appsettings.json`
- `appsettings.*.json`
- `deploy.secrets.bat`
- `*.pfx`, `*.p12` (certificates)
- `*.key`, `*.pem` (private keys)

### Secret Patterns
- Personal email addresses (gmail, yahoo, umindanao.edu.ph)
- Hardcoded passwords
- API keys
- Connection strings with passwords
- JWT secrets
- Cloud provider credentials (AWS, Azure, Google)

### Safe to Commit
- Bcrypt password hashes (`$2a$`, `$2b$`, `$2y$`)
- Anti-forgery tokens
- `appsettings.example.json`
- Migration files (without personal emails)

## 🐛 Troubleshooting

### Pre-commit hook not running
```powershell
# Reinitialize git hooks
git init
```

### False positive blocking commit
```bash
# Review the warning carefully
# If it's truly safe, use --no-verify
git commit --no-verify -m "your message"
```

### GitHub Actions failing
1. Check the Actions tab in GitHub
2. Review the error logs
3. Fix the detected issues
4. Push again

## 📚 Learn More

- Full documentation: `SECURITY.md`
- GitLeaks config: `.gitleaks.toml`
- GitHub workflow: `.github/workflows/security-scan.yml`

## 🎯 Common Scenarios

### Scenario 1: Accidentally staged sensitive file
```bash
# Unstage the file
git reset HEAD appsettings.json

# Verify it's in .gitignore
cat .gitignore | grep appsettings.json
```

### Scenario 2: Need to update Super Admin email
```bash
# DON'T put it in migrations
# Instead, update directly in database or use environment variable

# Option 1: Environment variable
$env:SUPER_ADMIN_EMAIL="your-email@example.com"

# Option 2: Update in database after deployment
UPDATE Users SET Email = 'your-email@example.com' WHERE Id = 1
```

### Scenario 3: Committed secret by mistake
```bash
# If not pushed yet
git reset --soft HEAD~1  # Undo commit, keep changes
# Remove the secret, then commit again

# If already pushed
# Contact repository admin immediately
# May need to rotate the compromised secret
```

## ✨ Best Practices

1. **Always review** `git diff` before committing
2. **Use environment variables** for all secrets
3. **Never commit** real credentials, even temporarily
4. **Test locally** before pushing
5. **Keep secrets in** Azure Key Vault or similar
6. **Rotate secrets** if accidentally exposed
7. **Enable GitHub push protection** for extra safety

---

**Need Help?** Check `SECURITY.md` or contact the security team.
