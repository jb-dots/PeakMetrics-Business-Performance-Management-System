# Security Policy

## Automated Security Scanning

This repository uses multiple layers of security scanning to prevent accidental exposure of sensitive information.

### 1. Pre-Commit Hooks (Local)

A pre-commit hook runs automatically before each commit to check for:
- Sensitive files (appsettings.json, deploy.secrets.bat, etc.)
- Personal email addresses in migration files
- Hardcoded passwords and API keys
- Connection strings with passwords

**Bypass (use with caution):**
```bash
git commit --no-verify -m "your message"
```

### 2. GitHub Actions (CI/CD)

Automated workflows run on every push and pull request:

#### Secret Scanning
- **TruffleHog**: Detects secrets in code and git history
- **GitLeaks**: Scans for hardcoded credentials
- **Custom patterns**: Checks for project-specific sensitive data

#### Dependency Scanning
- Checks for vulnerable NuGet packages
- Generates vulnerability reports

#### Code Quality
- SonarCloud analysis (optional, requires SONAR_TOKEN)
- Security hotspot detection

### 3. GitLeaks Configuration

Custom rules defined in `.gitleaks.toml`:
- Connection string passwords
- Email credentials
- API keys
- JWT secrets
- Private keys
- Cloud provider credentials (AWS, Azure, Google)
- Personal email addresses

**Allowlisted patterns:**
- Bcrypt password hashes (`$2a$`, `$2b$`, `$2y$`)
- Anti-forgery tokens
- Example configuration files

## Best Practices

### ✅ DO:
- Use environment variables for secrets
- Store credentials in Azure Key Vault or similar
- Use `appsettings.example.json` for templates
- Keep `deploy.secrets.bat` in `.gitignore`
- Use bcrypt hashes for seed data passwords
- Review changes before committing

### ❌ DON'T:
- Commit `appsettings.json` with real credentials
- Hardcode passwords or API keys in code
- Include personal email addresses in migrations
- Commit private keys or certificates
- Push secrets to public repositories

## Sensitive Files

These files are blocked by pre-commit hooks:
- `appsettings.json`
- `appsettings.Development.json`
- `appsettings.Production.json`
- `deploy.secrets.bat`
- `*.pfx`, `*.p12` (certificates)
- `*.key`, `*.pem` (private keys)

## Configuration Files

### Safe to Commit:
- `appsettings.example.json` - Template with placeholder values
- `deploy.bat` - Deployment script without secrets
- Migration files (without personal emails)

### Never Commit:
- `appsettings.json` - Contains real connection strings
- `deploy.secrets.bat` - Contains deployment credentials
- Any file with real passwords or API keys

## Environment Variables

Use environment variables for sensitive configuration:

```bash
# Windows (PowerShell)
$env:PEAKMETRICS_CONNECTION_STRING="your-connection-string"
$env:EmailSettings__Password="your-email-password"

# Linux/Mac
export PEAKMETRICS_CONNECTION_STRING="your-connection-string"
export EmailSettings__Password="your-email-password"
```

## GitHub Secrets Setup

For GitHub Actions to work properly, configure these secrets in your repository:

1. Go to **Settings** → **Secrets and variables** → **Actions**
2. Add the following secrets:

| Secret Name | Description | Required |
|-------------|-------------|----------|
| `SONAR_TOKEN` | SonarCloud authentication token | Optional |
| `GITLEAKS_LICENSE` | GitLeaks Pro license (if applicable) | Optional |

## Reporting Security Issues

If you discover a security vulnerability:

1. **DO NOT** open a public issue
2. Email the maintainers directly
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Potential impact
   - Suggested fix (if any)

## Security Checklist

Before pushing code:

- [ ] No hardcoded passwords or API keys
- [ ] No personal email addresses in migrations
- [ ] Sensitive files are in `.gitignore`
- [ ] Environment variables used for secrets
- [ ] Pre-commit hooks passed
- [ ] Reviewed `git diff` for sensitive data
- [ ] Connection strings use environment variables

## Additional Resources

- [OWASP Top 10](https://owasp.org/www-project-top-ten/)
- [.NET Security Best Practices](https://docs.microsoft.com/en-us/aspnet/core/security/)
- [GitHub Secret Scanning](https://docs.github.com/en/code-security/secret-scanning)
- [GitLeaks Documentation](https://github.com/gitleaks/gitleaks)
