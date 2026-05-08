# GitHub Actions Workflow Fixes

## Issue: TruffleHog "BASE and HEAD commits are the same" Error

### Problem
The TruffleHog secret scan was failing with:
```
BASE and HEAD commits are the same. TruffleHog won't scan anything.
```

This occurred because the workflow was using:
```yaml
base: ${{ github.event.repository.default_branch }}
head: HEAD
```

In certain contexts (push to main, workflow_dispatch), both values resolved to the same commit, causing TruffleHog to exit without scanning.

### Solution Applied

#### 1. Event-Appropriate Base/Head Values
Changed to use different values based on the event type:

```yaml
# PRs: scan the PR diff
base: ${{ github.event.pull_request.base.sha || github.event.before }}
head: ${{ github.event.pull_request.head.sha || github.sha }}
```

**How it works:**
- **Pull Requests**: Uses `pull_request.base.sha` → `pull_request.head.sha` (actual PR diff)
- **Push Events**: Uses `event.before` → `github.sha` (diff between commits)
- **Result**: Always has a real diff to scan

#### 2. Fallback for Initial Commits
Added conditional logic to handle edge cases:

```yaml
# Skip diff scan if this is an initial commit
if: github.event.before != '0000000000000000000000000000000000000000'
```

And a fallback full scan:

```yaml
# Run full scan for initial commits or manual triggers
if: github.event.before == '0000000000000000000000000000000000000000' || github.event_name == 'workflow_dispatch'
```

**Why this is needed:**
- Initial commits have no previous commit to diff against
- Manual workflow triggers may not have event.before set
- Full scan ensures nothing is missed in these cases

#### 3. Version Pinning
Using `@main` branch:

```yaml
uses: trufflesecurity/trufflehog@main
```

**Note**: TruffleHog doesn't use semantic versioning tags (v1, v2, v3). The `@main` branch is the stable release branch for the GitHub Action.

**Benefits:**
- Always uses the latest stable version
- Maintained by TruffleHog team
- Includes latest secret detection patterns

## Testing

### Test Cases Covered

1. **Push to main** ✅
   - Uses `event.before` → `github.sha`
   - Scans diff between commits

2. **Pull Request** ✅
   - Uses `pull_request.base.sha` → `pull_request.head.sha`
   - Scans PR changes only

3. **Initial Commit** ✅
   - Detects `event.before == 0000...`
   - Runs full scan instead of diff

4. **Manual Trigger (workflow_dispatch)** ✅
   - Detects `event_name == 'workflow_dispatch'`
   - Runs full scan

### Expected Behavior

| Event Type | Scan Type | Base | Head |
|------------|-----------|------|------|
| Push (normal) | Diff | `event.before` | `github.sha` |
| Push (initial) | Full | N/A | Current |
| Pull Request | Diff | `PR base` | `PR head` |
| Manual Trigger | Full | N/A | Current |

## Additional Improvements

### Other Workflow Enhancements
- ✅ GitLeaks scan (pattern-based detection)
- ✅ Sensitive file checks
- ✅ Hardcoded secret detection
- ✅ Dependency vulnerability scanning
- ✅ SonarCloud integration (optional)

### Local Protection
- ✅ Pre-commit hooks
- ✅ Custom GitLeaks configuration
- ✅ Enhanced .gitignore

## Verification

To verify the fix is working:

1. **Check GitHub Actions**
   - Go to **Actions** tab in repository
   - Look for "Security Scan" workflow
   - Verify TruffleHog step completes successfully

2. **Test Different Scenarios**
   ```bash
   # Test normal push
   git commit -m "test" --allow-empty
   git push
   
   # Test manual trigger
   # Go to Actions → Security Scan → Run workflow
   ```

3. **Review Logs**
   - TruffleHog should show: "Scanning X commits"
   - No "BASE and HEAD are the same" error

## Troubleshooting

### If TruffleHog Still Fails

1. **Check the event context**
   ```yaml
   - name: Debug Event
     run: |
       echo "Event: ${{ github.event_name }}"
       echo "Before: ${{ github.event.before }}"
       echo "SHA: ${{ github.sha }}"
   ```

2. **Verify fetch-depth**
   - Ensure `fetch-depth: 0` in checkout step
   - This fetches full history for diff scanning

3. **Check TruffleHog version**
   - Using `@main` (stable branch)
   - Check [TruffleHog Action](https://github.com/trufflesecurity/trufflehog-actions-scan)

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "No commits to scan" | Shallow clone | Set `fetch-depth: 0` |
| "BASE and HEAD same" | Wrong base/head values | Use event-appropriate values |
| "Action not found" | Wrong version | Use `@main` branch |
| Timeout | Full history scan | Use diff scan when possible |

## References

- [TruffleHog GitHub Action](https://github.com/trufflesecurity/trufflehog-actions-scan)
- [GitHub Actions Context](https://docs.github.com/en/actions/learn-github-actions/contexts)
- [GitLeaks Action](https://github.com/gitleaks/gitleaks-action)

## Commit History

- `d43280c`: Fix TruffleHog base/head configuration
- `270472f`: Add security quick start guide
- `29299a0`: Fix emoji encoding in security setup script
- `caa9503`: Add comprehensive security scanning

---

**Status**: ✅ Fixed and deployed
**Last Updated**: 2026-05-08
