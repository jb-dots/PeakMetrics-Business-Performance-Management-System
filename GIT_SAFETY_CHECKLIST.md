# Git Safety Checklist - Preventing Credential Leaks

## ✅ Current Protection Status

### Files Protected by .gitignore
- ✅ `appsettings.json` - **NEVER commit this!**
- ✅ `appsettings.Development.json`
- ✅ `appsettings.Production.json`
- ✅ `deploy.secrets.bat`

### Pre-Commit Hook Installed
- ✅ `.git/hooks/pre-commit.ps1` - Blocks sensitive files from being committed

## 📋 Before Every Commit - Quick Checklist

Run this command before committing:
```bash
git status
```

**Verify:**
- [ ] `appsettings.json` is NOT listed in "Changes to be committed"
- [ ] `deploy.secrets.bat` is NOT listed in "Changes to be committed"
- [ ] No files with passwords or secrets are staged

## 🔍 How to Check What You're About to Commit

### 1. Check Staged Files
```bash
git status
```

### 2. Review Changes Before Committing
```bash
git diff --cached
```

### 3. Verify Sensitive Files Are Ignored
```bash
git check-ignore -v appsettings.json
# Should output: .gitignore:27:appsettings.json  appsettings.json
```

## 🚫 What to Do If You Accidentally Stage a Sensitive File

### If You Haven't Committed Yet
```bash
# Unstage the file
git reset HEAD appsettings.json

# Verify it's unstaged
git status
```

### If You Already Committed (But Haven't Pushed)
```bash
# Remove from the last commit
git reset --soft HEAD~1

# Unstage the sensitive file
git reset HEAD appsettings.json

# Commit again without the sensitive file
git commit -m "Your commit message"
```

### If You Already Pushed to GitHub
1. **IMMEDIATELY** change all exposed credentials
2. Remove from Git tracking:
   ```bash
   git rm --cached appsettings.json
   git commit -m "Remove sensitive configuration"
   git push
   ```
3. Follow the steps in `SECURITY_INCIDENT_RESPONSE.md`

## 🛡️ Safe Git Workflow

### Step 1: Make Your Changes
Edit your code files as needed. It's safe to edit `appsettings.json` locally - it won't be committed.

### Step 2: Check What Changed
```bash
git status
```

### Step 3: Stage Only Code Files
```bash
# Stage specific files (RECOMMENDED)
git add Controllers/HomeController.cs
git add Views/Home/UserForm.cshtml

# OR stage all changes (but check status first!)
git add .
```

### Step 4: Verify Before Committing
```bash
# Check what's staged
git status

# Review the actual changes
git diff --cached
```

### Step 5: Commit
```bash
git commit -m "Your descriptive commit message"
```

### Step 6: Push
```bash
git push origin main
```

## 🎯 Quick Reference Commands

### Check if a file is ignored
```bash
git check-ignore -v appsettings.json
```

### See what's staged for commit
```bash
git status
git diff --cached
```

### Unstage a file
```bash
git reset HEAD filename
```

### Remove a file from Git tracking (keep local copy)
```bash
git rm --cached filename
```

## 🔐 Files That Should NEVER Be Committed

| File | Why | Status |
|------|-----|--------|
| `appsettings.json` | Contains database passwords, email credentials | ✅ Ignored |
| `appsettings.Development.json` | Contains dev credentials | ✅ Ignored |
| `appsettings.Production.json` | Contains production credentials | ✅ Ignored |
| `deploy.secrets.bat` | Contains deployment credentials | ✅ Ignored |
| `*.user` | User-specific IDE settings | ✅ Ignored |
| `*.suo` | Visual Studio user options | ✅ Ignored |

## 📝 Files That ARE Safe to Commit

| File | Why |
|------|-----|
| `appsettings.example.json` | Template with placeholder values |
| `deploy.secrets.example.bat` | Template for deployment secrets |
| `.gitignore` | Tells Git what to ignore |
| All `.cs` files | Source code (no secrets) |
| All `.cshtml` files | Views (no secrets) |
| `README.md` | Documentation |

## 🎓 Best Practices

1. **Always run `git status` before committing**
2. **Review changes with `git diff --cached`**
3. **Stage files individually when possible** (not `git add .`)
4. **Never commit files with passwords or API keys**
5. **Use environment variables for production secrets**
6. **Keep `.gitignore` up to date**

## ⚠️ Red Flags - Stop and Review

If you see any of these in `git status`, **STOP** and review:
- ❌ `appsettings.json`
- ❌ `deploy.secrets.bat`
- ❌ Any file with "secret", "password", or "key" in the name
- ❌ Any file you don't recognize

## 📞 When in Doubt

**If you're unsure whether a file should be committed:**
1. Check if it's in `.gitignore`
2. Check if it contains passwords or secrets
3. Ask yourself: "Would I want this on public GitHub?"
4. If still unsure, **DON'T commit it** - ask for help first!

## ✅ Verification

To verify your setup is secure:

```bash
# 1. Check .gitignore includes sensitive files
cat .gitignore | grep appsettings.json

# 2. Verify appsettings.json is ignored
git check-ignore -v appsettings.json

# 3. Check nothing sensitive is staged
git status

# 4. Review what would be committed
git diff --cached
```

All checks should pass before you push to GitHub!

---

**Remember: It's better to be overly cautious than to leak credentials!**
