# Linux QueryPlanInterop (QPI): test findings and action items

- **Branch:** `users/aavasthy/queryinteroplinux`
- **Linux native library:** `Microsoft.Azure.Cosmos.QueryPlanInterop.Linux` 1.0.5 (`libQueryPlanInterop.amd64.so`)
- **Windows native library:** ServiceInterop from `Microsoft.Azure.Cosmos.Direct` 3.44.1
- **Investigated:** 2026-10-23, on Windows and on Linux (WSL Ubuntu)

## Summary

| Area | Status |
|---|---|
| 8 publish tests failing on Linux | Fixed in the SDK, commit `e7f6a2e26` (on origin) |
| Existing query-plan test suites not running on Linux | Added to the Linux CI job, with test fixes, commit `da9690bf2` (section 2.2) |
| QPI: non-ASCII characters in the query | **Bug, blocks release.** The query fails with a 500 (1.1) |
| QPI: crash when `en_US.UTF-8` isn't installed | **Bug, blocks release.** The whole process aborts (1.2) |
| QPI: number formatting in hybrid-search queries | Needs QPI confirmation (1.3) |
| QPI: range order, extra JSON fields | Cosmetic (1.4, 1.5) |

Results with all SDK and test changes applied:

| Platform | Test cases | Passed | Skipped | Failed |
|---|---|---|---|---|
| Windows | 148 (CI filter without the 8 long-running publish tests) | 144 | 4 | 0 |
| Linux | 156 (full CI filter) | 146 | 4 | 6, all QPI differences from section 1 |

The 4 skipped tests are already marked as ignored in the code. For reference, the original CI run of
this branch had 9 of 71 tests failing on Linux: the Unicode test (1.1) and the 8 publish tests (2.1).

---

## The 6 Linux failures: QPI or SDK?

**All 6 are QPI issues; none is an SDK bug.** Each test fails because the Linux library's output
differs from what Windows ServiceInterop produces. Two are real QPI bugs, one needs the QPI team to
confirm, and three are harmless output differences.

| # | Failing test | QPI cause | Real bug? | Where to fix | Section |
|---|---|---|---|---|---|
| 1 | `QueryPlanBaselineTests.PointRange` | Non-ASCII text makes the query fail | **Yes** | QPI | 1.1 |
| 2 | `QueryPlanInteropTests.ProviderHandlesUnicodeConfigUpdateLargeOutputAndVectorPolicy` | Same non-ASCII bug | **Yes** | QPI | 1.1 |
| 3 | `QueryPlanRetrieverTests.SanityTests` | Writes `±1.79769e+308` as `±1.7976900000000001e+308` in hybrid-search queries | Probably not; QPI to confirm | QPI (match Windows), or the SDK test once confirmed harmless | 1.3 |
| 4 | `QueryPlanBaselineTests.ManyRanges` | Returns the same ranges in a different order | No | QPI, or the SDK test sorts the ranges | 1.4 |
| 5 | `QueryPlanBaselineTests.Top` | Adds two extra null/zero fields to the plan JSON | No | Same native version on both platforms, or QPI drops the fields | 1.5 |
| 6 | `QueryPlanBaselineTests.OffsetLimit` | Same extra fields | No | Same as #5 | 1.5 |

### Failure 1: `QueryPlanBaselineTests.PointRange`

- **What fails:** 2 of its 14 queries. The other 12 match Windows exactly.
  - `SELECT * FROM Root r WHERE r.key = '你好'`
  - `SELECT * FROM Root r WHERE r.key = 'こにちわ'`
- **Windows:** a plan with one single-key range for that value.
- **Linux:** `UnexpectedQueryPartitionProviderException` wrapping
  `ArgumentException: Value does not fall within the expected range.` (`E_INVALIDARG` with no error text).
- **Cause:** the QPI non-ASCII bug (1.1).
- **Fix:** in QPI. The test and its baseline are correct; no SDK change.

### Failure 2: `QueryPlanInteropTests.ProviderHandlesUnicodeConfigUpdateLargeOutputAndVectorPolicy`

- **What the test does:** creates a provider with `maxSqlQueryInputLength` = 5 and sends a long query
  whose property name (`café🙂`) and value (`東京🙂`) are non-ASCII. It expects the "exceeded the maximum
  limit of 5 characters" error, then raises the limit and expects a valid plan and a vector-search plan.
- **Linux:** the first call fails with `E_INVALIDARG` and no message, so the check for "maximum limit of
  5 characters" fails.
- **Cause:** 1.1. With an ASCII-only query, QPI returns the correct length-limit error.
- **Fix:** in QPI; no SDK change.

### Failure 3: `QueryPlanRetrieverTests.SanityTests`

The test checks 38 queries and stops at the first mismatch. I ran all 38 on Linux, recording each
mismatch instead of stopping: 5 differ, all hybrid-search queries with a vector component. Queries 36
and 38 repeat queries 31 and 37 with the hybrid-search query-plan optimization turned on.

| Query # in test | Query | Hybrid-search optimization |
|---|---|---|
| 31 | `SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, "swim", "run"), VectorDistance(c.image, [1, 2, 3]))` | Off |
| 32 | `SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.backup_image, [0.5, 0.2, 0.33]), VectorDistance(c.image, [1, 2, 3]))` | Off |
| 36 | Same as 31 | On |
| 37 | `SELECT TOP 10 * FROM c ORDER BY RANK RRF(VectorDistance(c.backup_image, [0.5, 0.2, 0.33]), VectorDistance(c.image, [1, 2, 3]), [1, -0.3])` | Off |
| 38 | Same as 37 | On |

- **Difference, for example in query 31:**
  - Windows: `(_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)`
  - Linux: `(_VectorScore(c.image, [1, 2, 3]) ?? -1.7976900000000001e+308)`
  - Queries 37 and 38 also contain the positive value: `?? 1.79769e+308` on Windows and
    `?? 1.7976900000000001e+308` on Linux.
- **Nothing else differs:** once `±1.79769e+308` and `±1.7976900000000001e+308` are treated as equal,
  all 5 queries match Windows exactly, and the other 33 match as they are.
- **Cause:** QPI writes numbers with 17 significant digits and Windows with 6 (1.3). Both strings are
  the same `double`.
- **Real bug?** Probably not, but the query text sent to the backend differs between platforms, so the
  QPI team should confirm.
- **Fix:** QPI matches the Windows formatting (preferred, so the backend gets identical query text from
  both platforms). If QPI confirms it's harmless and won't change it, the SDK test could compare plans
  with that number normalized.

### Failure 4: `QueryPlanBaselineTests.ManyRanges`

- **What fails:** 1 of its 20 queries: `SELECT * FROM c WHERE ABS(c.key) = 1`.
- **Windows:** ranges `[-1.0]`, then `[1.0]`. **Linux:** `[1.0]`, then `[-1.0]`.
- **Cause:** QPI returns the same ranges in a different order (1.4).
- **Real bug?** No. The SDK sorts the ranges before using them
  ([QueryPartitionProvider.cs](../Microsoft.Azure.Cosmos/src/Query/Core/QueryPlan/QueryPartitionProvider.cs#L173)),
  so queries behave the same.
- **Fix:** QPI returns the Windows order, or the baseline test sorts the ranges before writing them.

### Failures 5 and 6: `QueryPlanBaselineTests.Top` and `QueryPlanBaselineTests.OffsetLimit`

- **What fails:** the 6 queries that use `2147483648`, one more than `int.MaxValue`:
  - `Top`: `SELECT TOP 2147483648 c.name FROM c`, and the same query with
    `ORDER BY RANK FullTextScore(c.text, 'swim')`.
  - `OffsetLimit`: `SELECT c.name FROM c OFFSET 2147483648 LIMIT 10` and
    `SELECT c.name FROM c OFFSET 10 LIMIT 2147483648`, and both again with
    `ORDER BY RANK FullTextScore(c.text, 'swim')`.
- **Same behavior on both platforms:** the SDK rejects these plans because the value is above
  `int.MaxValue`, and the test records the raw plan JSON from the error. Only that recorded JSON differs.
- **Difference:** the Linux JSON has `"embeddingParameterMap":null` and `"requiresExtendedQueryPlan":0`;
  the Windows JSON doesn't.
- **Cause:** QPI is built from newer native code than the ServiceInterop in Direct 3.44.1 (1.5).
- **Real bug?** No. The parsed plans are identical, and the SDK behaves the same.
- **Fix:** goes away once both platforms ship the same native version (then regenerate these baselines
  on Windows), or QPI leaves out the two fields.

### Where the SDK problems were

None of the 6 failures is an SDK bug. The SDK problems I found were all in the test setup, and they're
fixed:

| SDK problem | Effect on Linux | Fixed in |
|---|---|---|
| Mixed-case NuGet package paths in the SDK project file | `dotnet pack` failed, so all 8 publish tests failed | 2.1, commit `e7f6a2e26` |
| The baseline harness used `\` in its folder paths | 23 baseline tests couldn't find their files | 2.2, commit `da9690bf2` |
| The baseline harness recorded .NET's OS-specific error text | 28 query differences | 2.2, commit `da9690bf2` |
| `TestBypassQueryParsing` assumed Windows defaults | 1 test failed | 2.2, commit `da9690bf2` |

---

## 1. QPI issues

### 1.1 Non-ASCII characters in the query fail with `E_INVALIDARG` (blocks release)

**Symptom**

```
UnexpectedQueryPartitionProviderException: Query service interop parsing hit an unexpected exception
---> System.ArgumentException: Value does not fall within the expected range.
```

`GetPartitionKeyRangesFromQuery4` returns `E_INVALIDARG` (`0x80070057`) with an empty output buffer
whenever the query text contains a non-ASCII character anywhere: `é`, `東京`, `你好`, an emoji, and so on.

**Root cause (inferred from behavior)**

QPI converts the UTF-32 `wchar_t` query text to UTF-8 using the C runtime's current locale
(`LC_CTYPE`). .NET never calls `setlocale`, so inside a .NET process `LC_CTYPE` stays `"C"`, which is
ASCII only, whatever `LANG` is set to. The conversion fails on the first non-ASCII character.

**Evidence** (calling `libQueryPlanInterop.amd64.so` directly from a small probe app)

| Call | Result |
|---|---|
| ASCII query, `maxSqlQueryInputLength` = 5 | `0x800A0B00` with `SC3020` "exceeded the maximum limit of 5 characters" (correct) |
| Query containing `é`, `東京` or an emoji, at any length limit | `0x80070057`, output length 0 |
| Same non-ASCII query after `setlocale(LC_CTYPE, "C.UTF-8")` | `S_OK`, valid plan |
| After switching back with `setlocale(LC_CTYPE, "C")` | `0x80070057`, output length 0 |

Windows isn't affected: ServiceInterop takes UTF-16 strings and doesn't depend on the C locale.

**Customer impact (opt-in enabled):** any query with a non-ASCII character in its text fails with a
500 `CosmosException`. The SDK creates an `UnexpectedQueryPartitionProviderException`
([QueryPartitionProvider.cs](../Microsoft.Azure.Cosmos/src/Query/Core/QueryPlan/QueryPartitionProvider.cs#L301)),
which becomes an InternalServerError
([ExceptionToCosmosException.cs](../Microsoft.Azure.Cosmos/src/Query/Core/ExceptionToCosmosException.cs#L130)).
There's no fallback to the gateway.

**Failing tests**
- `QueryPlanBaselineTests.PointRange`: `SELECT * FROM Root r WHERE r.key = '你好'` and `... r.key = 'こにちわ'`
- `QueryPlanInteropTests.ProviderHandlesUnicodeConfigUpdateLargeOutputAndVectorPolicy`
  ([QueryPlanInteropTests.cs](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Query/QueryPlanInteropTests.cs#L113)).
  The test expects the length-limit message, but its property name and value are non-ASCII, so it
  hits this bug first.

**Fix to request from QPI**
- Encode UTF-32 to UTF-8 explicitly, without using the C locale.
- For bad input, return an error message in the output buffer instead of a bare `E_INVALIDARG`.

### 1.2 The process aborts when the `en_US.UTF-8` locale isn't installed (blocks release)

**Symptom**

```
libc++abi: terminating due to uncaught exception of type std::runtime_error:
collate_byname<char>::collate_byname failed to construct for en_US.UTF-8
```

The whole process ends, and .NET can't catch it.

**Details**
- **When it happens:** on the first `GetPartitionKeyRangesFromQuery4` call, even for a pure-ASCII query.
- **Why the SDK doesn't catch it:** `CreateServiceProvider` succeeds, so the SDK's availability check
  (`LinuxLibraryAvailable`,
  [QueryPlanInterop.cs](../Microsoft.Azure.Cosmos/src/Query/Core/QueryPlan/QueryPlanInterop.cs#L16))
  marks the library as usable. The crash comes on the first real query.
- **The locale name is hard-coded:** the WSL machine had `LANG=C.UTF-8`, and QPI still asked for
  `en_US.UTF-8`.
- **Who is affected:** any Linux host or container that doesn't have `en_US.UTF-8` generated, such as
  slim Docker images and minimal distros. The Azure Pipelines Ubuntu agents have it, so CI doesn't
  show this.

**Fix to request from QPI**
- Don't depend on a named locale; compare and collate without one.
- Catch all C++ exceptions at the `extern "C"` functions and return an `HRESULT` instead.

### 1.3 Numbers are formatted differently in hybrid-search queries (needs QPI confirmation)

**Test:** `QueryPlanRetrieverTests.SanityTests`, 5 of its 38 queries. All are hybrid-search queries
(`ORDER BY RANK RRF(...)`) with a vector component, for example
`SELECT TOP 10 * FROM c ORDER BY RANK RRF(FullTextScore(c.text, "swim", "run"), VectorDistance(c.image, [1, 2, 3]))`.
See "Failure 3" above for the full list.

| Platform | Fragment of the rewritten query |
|---|---|
| Windows | `(_VectorScore(c.image, [1, 2, 3]) ?? -1.79769e+308)` |
| Linux | `(_VectorScore(c.image, [1, 2, 3]) ?? -1.7976900000000001e+308)` |

Queries with RRF weights (`[1, -0.3]`) also contain the positive value: `1.79769e+308` on Windows and
`1.7976900000000001e+308` on Linux.

Windows prints numbers with 6 significant digits and Linux with 17. Both strings are the same
`double`, and with the number treated as equal, all 5 queries match Windows exactly. But the query text
sent to the backend differs, so ask QPI to confirm this is harmless, or to match the Windows formatting.

### 1.4 Partition ranges come back in a different order (cosmetic)

**Test:** `QueryPlanBaselineTests.ManyRanges`, query `SELECT * FROM c WHERE ABS(c.key) = 1`

Windows returns the ranges `[-1.0]` then `[1.0]`; Linux returns `[1.0]` then `[-1.0]`. It's the same
set of ranges. The SDK sorts ranges before using them
([QueryPartitionProvider.cs](../Microsoft.Azure.Cosmos/src/Query/Core/QueryPlan/QueryPartitionProvider.cs#L173)),
so nothing changes at runtime. The baseline test fails only because it compares the order.

**Options:** QPI matches the Windows order, or the baseline test sorts the ranges before writing them.

### 1.5 Two extra fields in the plan JSON (cosmetic, version difference)

**Tests:** `QueryPlanBaselineTests.Top` (2 queries) and `QueryPlanBaselineTests.OffsetLimit` (4 queries).
Only queries that use `2147483648` for TOP, OFFSET or LIMIT are affected. The SDK rejects those values
because they're above `int.MaxValue`, and the test records the raw plan JSON in the error message.

The Linux JSON includes `"embeddingParameterMap":null` and `"requiresExtendedQueryPlan":0`. The
ServiceInterop in Direct 3.44.1 doesn't write these fields, because QPI is built from newer native
code. The parsed plans are identical.

**Goes away when:** ServiceInterop and QPI ship from the same native code version. Then regenerate
the baselines once on Windows.

### 1.6 QPI writes to the console (minor)

On every query compile error, QPI prints a line like this to the console, which would end up in
customer logs:

```
FAILED at /workspaces/CosmosDB/Product/Microsoft.Azure.Documents/ServiceInterop/ServiceInteropImpl.cpp:97 with 0x800a0b00.
```

### Checked and fine

- **Options struct:** the native function takes a 64-byte struct by value, which matters for the Linux
  calling convention. The managed `ServiceInteropWrapper.PartitionKeyRangesApiOptions` is also 64
  bytes, so they match.
- **String encoding:** UTF-8 config strings and UTF-32 query, path-token and vector-policy strings,
  as the QPI header specifies. All 60 other Linux interop tests pass with these.

---

## 2. SDK and test changes

### 2.1 Committed (`e7f6a2e26`): the SDK can be packed on Linux

- **Problem:** the pack items for the Direct and HybridRow DLLs built NuGet cache paths with
  mixed-case folder names (`$(NugetPackageRoot)\Microsoft.HybridRow\...`), but NuGet's cache folders
  are lowercase. Windows ignores case; Linux doesn't. `dotnet pack` failed in
  `CosmosTargetsInteropPublishTests.ClassInitialize`, so all 8 publish tests failed at 0 ms.
- **Fix:** `GeneratePathProperty="true"` on both package references, and the pack items now use
  `$(PkgMicrosoft_HybridRow)` and `$(PkgMicrosoft_Azure_Cosmos_Direct)`
  ([Microsoft.Azure.Cosmos.csproj](../Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L121),
  [pack items](../Microsoft.Azure.Cosmos/src/Microsoft.Azure.Cosmos.csproj#L172)). This branch already
  used the same approach for the QPI package.
- **Verified:**
  - Windows: the package built before and after has the same 17 entries with identical sizes.
  - Linux: all 8 publish tests pass, including the steps that load the `.so` and check the fallback
    to the gateway.

### 2.2 Committed (`da9690bf2`): run the existing query-plan suites on Linux

| File | Change | Why | Effect on Windows |
|---|---|---|---|
| [azure-pipelines.yml](../azure-pipelines.yml#L42) | The Linux job's filter adds 7 existing suites | Linux coverage | None; only the Linux job changes |
| [BaselineTests.cs](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/BaselineTest/BaselineTests.cs#L29) | Baseline folders built with `Path.Combine` | Hard-coded `\` paths meant 23 tests couldn't find their baseline files on Linux | Same path string |
| [QueryPlanBaselineTests.cs](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Query/QueryPlanBaselineTests.cs#L1848) | Native errors recorded as `0x{HResult:X8}` | .NET writes the error message differently on each OS (`0x800A0B00` vs `Error 2148141824 (0x800A0B00)`); this caused 28 differences | Identical text; no baseline file changed |
| [QueryPlanRetrieverTests.cs](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Query/QueryPlanRetrieverTests.cs#L69) | `TestInitialize`/`TestCleanup` set `AZURE_COSMOS_USE_QUERY_INTEROP=true` and restore it afterwards; [mixed-case rows skipped](../Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Query/QueryPlanRetrieverTests.cs#L220) when not on Windows | Local planning is opt-in on Linux, and Linux treats environment variable names as case-sensitive | Windows ignores the variable, and all rows still run there |

**Diff size:** 29 lines added and 4 removed across the 4 files.

**Results:**
- Linux: 56 of the 85 existing tests passed before these fixes. With them, the full CI filter gives
  146 of 156 passing, and the 6 failures are issues 1.1, 1.3, 1.4 and 1.5.
- Windows: unchanged, 0 failures.

**Careful when editing:** `QueryPlanBaselineTests.cs` has a BOM and mixed CRLF/LF line endings. The
VS Code edit tool rewrites the whole file to LF, which shows up as a diff of about 175 lines. Check
with `git diff --numstat` before committing.

### 2.3 What the Linux CI job runs now

| Test class | Size |
|---|---|
| `QueryPlanInteropTests` (new on this branch) | 61 test cases |
| `QueryPartitionProviderTests` | 2 |
| `CosmosTargetsInteropPublishTests` | 8 test cases |
| `QueryPlanBaselineTests` | 20 methods, about 175 queries |
| `OptimisticDirectExecutionQueryBaselineTests` | 18 |
| `FullPipelineTests` | 12 |
| `PageSizeFactorForTopTests` | 9 methods (14 data rows) |
| `QueryPlanRetrieverTests` | 6 |
| `PartitionRoutingHelperTest` | 6 |
| `SubpartitionTests` | 2 |

**Still not run on Linux:**
- Emulator tests (the emulator is Windows-only)
- `DirectContractTests` and `ContractEnforcementTests` (`TestCategory("Windows")`)
- `TraceWriterBaselineTests`, which uses the provider in one scenario

---

## 3. Action items

**QPI team**
- [ ] 1.1 Encode non-ASCII text to UTF-8 without the C locale (blocks release)
- [ ] 1.2 Remove the `en_US.UTF-8` dependency and catch exceptions at the exported functions (blocks release)
- [ ] 1.3 Confirm the number formatting in hybrid-search queries, or match Windows
- [ ] 1.4 and 1.5 Optional: match the Windows range order and output fields
- [ ] 1.6 Remove the console output

**SDK (this branch)**
- [x] Commit the section 2.2 changes (4 files): `da9690bf2`.
- [ ] Decide how CI handles the 6 known failures until a fixed QPI ships: leave the Linux job red, or
      tag them (for example `TestCategory("LinuxQpiKnownIssue")`), exclude that tag in the Linux
      filter, and remove the tag once fixed.
- [ ] If the opt-in ships before the QPI fixes:
  - add the limitations (non-ASCII queries unsupported, `en_US.UTF-8` required) to the Linux section
    of the [README](../README.md#L41);
  - consider falling back to the gateway when the native call fails with no output. That avoids the
    500 from 1.1, but doesn't help the crash in 1.2.
- [ ] When a fixed QPI is available, bump `QueryPlanInteropLinuxVersion` in `Directory.Build.props`
      and re-run the Linux suite. The tests for 1.1 should then pass.
- [ ] Optional cleanups:
  - the test csproj builds nuspec paths with mixed-case folder names (lines 75 and 78). They're
    skipped on Linux only because the job passes `/p:OS=Linux`.
  - `Microsoft.Azure.Cosmos.EmulatorTests/BaselineTest/BaselineTests.cs` (line 32) has the same
    backslash paths; emulator tests don't run on Linux.

---

## 4. Reproducing on Linux (WSL)

1. Install .NET into the WSL home folder (no root needed):
   ```bash
   curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
   bash /tmp/dotnet-install.sh --channel 8.0 --install-dir ~/.dotnet
   bash /tmp/dotnet-install.sh --channel 6.0 --runtime dotnet --install-dir ~/.dotnet
   ```
2. Clone the branch inside WSL. That keeps the Windows `obj/` folders untouched.
   ```bash
   git clone --depth 1 --branch users/aavasthy/queryinteroplinux file:///mnt/<drive>/<path-to-your-windows-clone> ~/qpi-repo
   ```
   To test changes that aren't committed, run `git diff --binary > wip.patch` on Windows, then
   `git apply wip.patch` inside the clone.
3. Environment settings. These are only needed on a bare WSL Ubuntu; CI agents don't need them.
   ```bash
   export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
   export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1   # libicu isn't installed
   export RunAnalyzers=false                        # StyleCop fails in invariant-globalization mode
   mkdir -p ~/qpi-locales && localedef -i en_US -f UTF-8 ~/qpi-locales/en_US.UTF-8
   export LOCPATH=~/qpi-locales                     # works around QPI issue 1.2
   ```
4. Build and test the way CI does:
   ```bash
   cd ~/qpi-repo
   dotnet build Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Microsoft.Azure.Cosmos.Tests.csproj -c Release /p:OS=Linux
   F=$(grep -o 'FullyQualifiedName~[^"]*' azure-pipelines.yml | head -1)
   dotnet test Microsoft.Azure.Cosmos/tests/Microsoft.Azure.Cosmos.Tests/Microsoft.Azure.Cosmos.Tests.csproj -c Release /p:OS=Linux --no-build --filter "$F"
   ```

**Tips**
- Run the commands from a script file. Quoting breaks when PowerShell passes them through `wsl bash -c`.
- Keep logs in `~`, because WSL clears `/tmp` when the distro restarts.
- The baseline harness writes Linux output to `bin/Release/net6.0/BaselineTest/TestOutput/`. Compare
  it with `BaselineTest/TestBaseline/` to see exactly which query differs.
