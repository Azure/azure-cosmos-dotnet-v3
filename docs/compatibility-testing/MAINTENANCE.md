# Maintenance Guide - Encryption.Custom Compatibility Testing

This guide provides checklists and procedures for maintaining the compatibility testing framework over time.

---

## 📅 Regular Maintenance Schedule

| Frequency | Task | Owner | Est. Time |
|-----------|------|-------|-----------|
| **Monthly** | Check for new published versions | Team Lead | 5 min |
| **Quarterly** | Review and update version matrix | Team | 30 min |
| **Quarterly** | Clean up old test results | Developer | 10 min |
| **Quarterly** | Review and update API suppressions | Team | 15 min |
| **Annually** | Review entire test suite | Team | 2 hours |
| **Annually** | Update dependencies | Developer | 1 hour |
| **As needed** | Add new version to matrix | Developer | 20 min |
| **As needed** | Handle breaking changes | Team | Variable |

---

## 🆕 When a New Version is Published

### Checklist: Adding New Version to Test Matrix

Use this checklist whenever Microsoft.Azure.Cosmos.Encryption.Custom releases a new version:

#### 1. Discover New Version (5 min)

```powershell
# Check for new versions on NuGet
.\tools\discover-published-versions.ps1

# Output will show if newer versions are available
# Example:
# ⚠️  Baseline is outdated!
#     Current baseline: 1.0.0-preview07
#     Latest version: 1.0.0-preview08
```

**Action:**
- [ ] Note the new version number
- [ ] Check release notes on GitHub/NuGet

#### 2. Add Version to Test Matrix (5 min)

```powershell
# Add new version to test matrix
.\tools\update-test-matrix.ps1 -Version "1.0.0-preview08"

# If this is now the latest stable/preview, also set as baseline
.\tools\update-test-matrix.ps1 -Version "1.0.0-preview08" -SetBaseline
```

**Action:**
- [ ] Script updates `testconfig.json`
- [ ] Verify output shows version added
- [ ] Note the baseline version

#### 3. Update Pipeline YAML (10 min)

**File:** `azure-pipelines-encryption-custom-compatibility.yml`

**If you set a new baseline:**

```yaml
# Line ~8: Update variable
variables:
  BuildConfiguration: 'Release'
  BaselineVersion: '1.0.0-preview08'  # ← Update this

# Lines ~30-45: Update Stage 1
- stage: QuickCompatibilityCheck
  displayName: 'Quick Compatibility Check (PR only)'
  condition: eq(variables['Build.Reason'], 'PullRequest')
  jobs:
    - job: QuickCheck
      displayName: 'Test against baseline (preview08)'  # ← Update display name
      pool:
        vmImage: 'windows-latest'
      steps:
        - template: templates/encryption-custom-compatibility-test-steps.yml
          parameters:
            BuildConfiguration: $(BuildConfiguration)
            TestVersion: '1.0.0-preview08'  # ← Update this
```

**Add new job to Stage 2 (Full Matrix):**

```yaml
# Add after existing version jobs
- job: Test100preview08
  displayName: 'Test vs 1.0.0-preview08'
  pool:
    vmImage: 'windows-latest'
  steps:
    - template: templates/encryption-custom-compatibility-test-steps.yml
      parameters:
        BuildConfiguration: $(BuildConfiguration)
        TestVersion: '1.0.0-preview08'
```

**Action:**
- [ ] Update `BaselineVersion` variable if new baseline
- [ ] Update Stage 1 `TestVersion` if new baseline
- [ ] Add new job to Stage 2
- [ ] Save and commit changes

#### 4. Test Locally (10 min)

```powershell
# Test the new version locally
cd Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
.\test-compatibility.ps1 -Version "1.0.0-preview08"

# Review results
# Expected: ~23/30 tests passing (77%)
```

**Action:**
- [ ] Tests run successfully
- [ ] Pass rate similar to other versions (~77%)
- [ ] No unexpected new failures
- [ ] If issues, see [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

#### 5. Run API Compatibility Check (5 min)

```powershell
# Check for API breaking changes
.\tools\test-api-compat-local.ps1 -BaselineVersion "1.0.0-preview08" -ComparisonVersion "1.0.0-preview07"

# Review output
# Look for: "✅ No breaking changes" or list of changes
```

**Action:**
- [ ] No breaking changes detected, OR
- [ ] Breaking changes documented in API-CHANGES.md
- [ ] New suppressions added if needed

#### 6. Commit and Push (5 min)

```powershell
# Stage changes
git add testconfig.json
git add azure-pipelines-encryption-custom-compatibility.yml
git add Microsoft.Azure.Cosmos.Encryption.Custom/ApiCompatSuppressions.txt  # If updated
git add docs/compatibility-testing/API-CHANGES.md  # If updated

# Commit
git commit -m "chore: Add compatibility tests for v1.0.0-preview08"

# Push
git push origin main
```

**Action:**
- [ ] Changes committed
- [ ] Pushed to main branch
- [ ] Pipeline triggered automatically

#### 7. Monitor Pipeline (15 min)

1. Go to Azure DevOps Pipelines
2. Find triggered run
3. Monitor stages:
   - ✅ Stage 0: API Compatibility Check
   - ✅ Stage 1: Quick Check (PR only, may be skipped)
   - ✅ Stage 2: Full Matrix (including new version)
   - ✅ Stage 3: Report

**Action:**
- [ ] All stages pass
- [ ] New version job completes successfully
- [ ] Test results published
- [ ] Artifacts uploaded
- [ ] If failures, investigate and fix

#### 8. Update Documentation (10 min)

Update version references in:
- [ ] `QUICKSTART.md` - Update version in examples if baseline changed
- [ ] `README.md` - Update supported versions list
- [ ] `API-CHANGES.md` - Document any API changes

---

## 🧹 Quarterly Maintenance Tasks

### Q1: January-March

#### Review Version Matrix (30 min)

```powershell
# Check current matrix
Get-Content Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests\testconfig.json | ConvertFrom-Json

# Discover latest versions
.\tools\discover-published-versions.ps1 -Count 15
```

**Action:**
- [ ] Remove very old versions (>1 year old)
- [ ] Keep at least 4-6 recent versions
- [ ] Update baseline to latest stable/preview
- [ ] Test matrix updated

**Remove old version:**

```powershell
.\tools\update-test-matrix.ps1 -Version "1.0.0-preview01" -Remove
```

Then remove corresponding job from `azure-pipelines-encryption-custom-compatibility.yml`.

#### Clean Up Test Artifacts (10 min)

```powershell
# Remove old local test results
Remove-Item -Recurse TestResults_Custom -ErrorAction SilentlyContinue
Remove-Item -Recurse coverage -ErrorAction SilentlyContinue

# Clean build artifacts
dotnet clean Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests
```

**Action:**
- [ ] Local test results cleared
- [ ] Build artifacts cleaned
- [ ] Git status clean

### Q2: April-June

#### Review API Compatibility Suppressions (15 min)

```powershell
# Review suppression file
Get-Content Microsoft.Azure.Cosmos.Encryption.Custom\ApiCompatSuppressions.txt
```

**Action:**
- [ ] Remove suppressions for very old versions no longer tested
- [ ] Verify suppressions still valid
- [ ] Update justifications if needed
- [ ] Test API compat still passes

#### Update Documentation (20 min)

**Action:**
- [ ] Review all markdown files for accuracy
- [ ] Update version numbers in examples
- [ ] Fix broken links
- [ ] Update screenshots if any
- [ ] Spelling/grammar check

### Q3: July-September

#### Dependency Updates (1 hour)

```powershell
# Check for outdated packages
dotnet list Microsoft.Azure.Cosmos.Encryption.Custom\tests\Microsoft.Azure.Cosmos.Encryption.Custom.CompatibilityTests package --outdated

# Update test framework
# In Directory.Packages.props, update:
# - xunit
# - FluentAssertions
# - Microsoft.NET.Test.Sdk
```

**Action:**
- [ ] xUnit updated (if available)
- [ ] FluentAssertions updated (if available)
- [ ] Test SDK updated (if available)
- [ ] API compat tool updated (if available)
- [ ] All tests still pass after updates

#### Performance Review (30 min)

**Action:**
- [ ] Review pipeline execution times
- [ ] Identify slow tests
- [ ] Optimize if needed
- [ ] Document findings

### Q4: October-December

#### Full Test Suite Review (2 hours)

**Action:**
- [ ] Review each test class
- [ ] Add tests for new features
- [ ] Remove obsolete tests
- [ ] Update expected pass rates
- [ ] Document known limitations
- [ ] Test coverage assessment

---

## 🔥 Emergency Procedures

### Pipeline Completely Broken

**Symptoms:** All builds failing, no recent code changes

**Quick Fix:**

1. **Disable pipeline temporarily:**
   - Azure DevOps → Pipelines → Settings → Disable
   - Prevents blocking other work

2. **Investigate:**
   ```powershell
   # Check if it's a NuGet issue
   Invoke-RestMethod https://api.nuget.org/v3/index.json
   
   # Check if version disappeared
   .\tools\discover-published-versions.ps1
   
   # Review recent pipeline logs
   ```

3. **Fix quickly:**
   - If NuGet outage: Wait or use package cache
   - If version issue: Update testconfig.json
   - If syntax error: Validate YAML

4. **Re-enable pipeline:**
   - Azure DevOps → Pipelines → Settings → Enable

### Too Many False Positives in API Compat

**Symptoms:** API compat stage fails with dozens of "breaking changes" that aren't really breaking

**Quick Fix:**

1. **Skip API compat temporarily:**
   ```yaml
   # In azure-pipelines YAML
   - stage: ApiCompatibilityCheck
     condition: false  # ← Add this line temporarily
   ```

2. **Investigate offline:**
   ```powershell
   .\tools\test-api-compat-local.ps1 -BaselineVersion "X.X.X" -ComparisonVersion "Y.Y.Y"
   ```

3. **Add suppressions:**
   - Edit `ApiCompatSuppressions.txt`
   - Add entries for false positives
   - Document why each is suppressed

4. **Re-enable:**
   - Remove `condition: false`
   - Verify passes

### Critical Bug Found in Old Version

**Symptoms:** Need to immediately stop testing against a specific version

**Quick Fix:**

1. **Remove from matrix:**
   ```powershell
   .\tools\update-test-matrix.ps1 -Version "1.0.0-preview04" -Remove
   ```

2. **Update pipeline:**
   - Remove job from Stage 2 in YAML

3. **Document:**
   - Add note to README.md
   - Update SECURITY.md if security issue
   - Link to CVE/issue

---

## 📊 Metrics to Track

Track these metrics quarterly:

| Metric | Target | How to Measure |
|--------|--------|----------------|
| Pipeline success rate | >95% | Azure DevOps analytics |
| Average pipeline duration | <30 min | Azure DevOps analytics |
| Test pass rate | 75-80% | Test results |
| Versions in matrix | 4-6 | testconfig.json |
| Age of oldest version | <1 year | testconfig.json dates |
| API compat false positives | <5 | Suppression file entries |
| Documentation updates | Quarterly | Git log |

---

## 📚 Knowledge Transfer

### Onboarding New Maintainers

**Checklist for new team members:**

- [ ] Read [QUICKSTART.md](./QUICKSTART.md)
- [ ] Run tests locally successfully
- [ ] Read [PIPELINE-GUIDE.md](./PIPELINE-GUIDE.md)
- [ ] Review a recent pipeline run
- [ ] Read [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
- [ ] Shadow experienced maintainer adding new version
- [ ] Add a version to matrix independently
- [ ] Review this maintenance guide
- [ ] Understand quarterly schedule

**Resources:**
- 📖 All documentation: `docs/compatibility-testing/`
- 🛠️ All scripts: `tools/`
- ⚙️ Pipeline: `azure-pipelines-encryption-custom-compatibility.yml`
- 🧪 Tests: `Microsoft.Azure.Cosmos.Encryption.Custom/tests/`

---

## 🔗 Related Documentation

- [QUICKSTART.md](./QUICKSTART.md) - Getting started
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) - Common issues
- [PIPELINE-GUIDE.md](./PIPELINE-GUIDE.md) - Pipeline deep dive
- [API-CHANGES.md](./API-CHANGES.md) - API compatibility
- [CHEATSHEET.md](./CHEATSHEET.md) - Quick commands

---

**Last Updated:** 2025-01-XX  
**Next Review:** Q1 2025  
**Maintained By:** Cosmos Encryption Team
