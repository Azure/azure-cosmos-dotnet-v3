# Direct implementation

This component follows the FaultInjection layout: `Direct\src` is outside the
Client project's `src` directory. Imported implementation files retain their
upstream bytes and namespaces. Git line-ending conversion is disabled for those
files.

The import is pinned to CosmosDB release branch
`sdkReleases/direct/EN20260409-3.44.1`, commit
`743dc32433dc51e3d5357934014154419ae812aa`. `source-manifest.json` records each
original path, local path and SHA-256 hash. It includes the release project's
explicit managed/shared inputs, project-local C# sources, resources, four T4
templates, notices and the common assembly metadata template. MSBuild evaluation
of an isolated pinned release snapshot reconciles all 371 source/resource inputs
and two generated metadata inputs. The latter are replaced by the imported
`AssemblyRef` template, SDK assembly metadata and local version generation.
This is an input-inventory check, not a full internal release build.

The initial import does not preserve upstream Git blame. Use its recorded
revision and original paths for upstream history. Keep subsequent file moves
separate from implementation edits.

No CosmosDB DLLs or native C++ source are imported. Build integration must use
NuGet for the native query-plan engine and must not reference files or build
outputs from a CosmosDB checkout. The existing HybridRow binary dependency is
outside this migration's scope.

## Standalone build

```powershell
dotnet build Microsoft.Azure.Cosmos\Direct\src\Microsoft.Azure.Cosmos.Direct.csproj -c Release
```

The project produces the existing `Microsoft.Azure.Cosmos.Direct` assembly, not a
new public NuGet package. It retains the 3.44.1 assembly identity, signing key,
internal namespaces, friend assemblies and resource name. The upstream generated
`AssemblyVersionInfo` type is generated locally from `DirectVersion`.

Native query planning uses `Microsoft.Azure.Cosmos.QueryPlanInterop.Windows`
**1.0.5**, acquired exclusively through NuGet. Only its Windows x64 DLL is used,
renamed to `Microsoft.Azure.Cosmos.ServiceInterop.dll` for the existing P/Invoke
contract. No `.lib`, `.pdb`, CRTCompat or Visual C++ runtime DLL is copied from
CosmosDB. Existing platform-selection logic is unchanged: supported Windows
processes use native planning; other platforms use the existing service query-plan
path. Copying the x64 asset for a Windows RID does not add ARM64 or x86 native
support.

The SDK package contains these DLLs; the project-reference refactor adds none:

| DLL | Source |
| --- | --- |
| `Microsoft.Azure.Cosmos.Client.dll` | Existing Client project |
| `Microsoft.Azure.Cosmos.Direct.dll` | Local Direct project, replacing the managed Direct NuGet payload |
| `Microsoft.Azure.Cosmos.ServiceInterop.dll` | `Cosmos.QueryPlanInterop.dll` from QueryPlanInterop.Windows 1.0.5, renamed |
| `Microsoft.Azure.Cosmos.Core.dll` | Existing Microsoft.HybridRow NuGet dependency |
| `Microsoft.Azure.Cosmos.Serialization.HybridRow.dll` | Existing Microsoft.HybridRow NuGet dependency |

Direct retains the original managed package requirements: Newtonsoft.Json 10.0.2,
System.Configuration.ConfigurationManager 6.0.0,
System.Diagnostics.DiagnosticSource 6.0.1, System.Memory 4.5.5 and
System.Threading.Tasks.Extensions 4.5.4. NuGet resolves their transitive framework
dependencies normally; they are not copied from CosmosDB or bundled as additional
DLLs inside the SDK package. The Client already declares these dependencies and
requires DiagnosticSource 8.0.1. Newtonsoft.Json remains an explicit consumer
requirement. NETStandard.Library 2.0.3 is an implicit, private build dependency of
the netstandard2.0 Direct project.

## Imported assembly metadata and suppressions

Direct is a separate assembly. The Client's `AssemblyInfo.cs` and
`GlobalSuppressions.cs` apply to Client, not automatically to Direct.

| Imported file | Purpose and reuse decision |
| --- | --- |
| `Properties/AssemblyInfoCommon.cs` | Defines Direct's friend-assembly grants and global `AssemblyKeys` constants. Client and FaultInjection also consume those constants through Direct. Replacing it with the Client's assembly metadata would give Direct the wrong friend list. |
| `Properties/AssemblyRef.cs` | Defines the global `AssemblyRef` key constants used by Direct's friend declarations. `AssemblyInfo_PublicKeyRefOnly` disables the upstream product-metadata template section; SDK-generated metadata supplies that part instead. There is no equivalent global helper elsewhere in v3. |
| `Properties/GlobalSuppressions.cs` | Preserves upstream CA-rule suppressions for specific Direct types and members. It is not a runtime requirement. Default Direct builds have `EnableNETAnalyzers=false`; the file is retained for source fidelity and the upstream analyzer baseline, not because Client's style suppressions need to be duplicated. |

The existing `.snk` file is already reused for signing. It is not a replacement
for the public-key constants in `InternalsVisibleTo` declarations. Encryption's
mirrored `AssemblyKeys` helpers are namespaced and contain only the test key;
they are not drop-in replacements for Direct's global helpers.

Keep all three imported files unchanged for this source-preserving migration.
Their contents could be reorganized within Direct in a separate cleanup, but
removing or renaming the key helper types changes the internal assembly contract.
Moving Direct's suppressions into Client's suppression file would not apply them
to Direct; broadening repository-wide analyzer rules is also not equivalent.

## Native compatibility validation

Native package 1.0.5 passes 19 of the 21 targeted query-plan and ODE validity
tests in both Debug and Release preview builds. This includes `NonValueAggregates`,
`GroupBy`, `Negative`, `PointRange`, `Spatial` and
`TestQueryValidityCheckWithODEAsync`, which failed with 1.0.2.

`QueryPlanBaselineTests.Top` and `OffsetLimit` still fail exact-output comparison.
The only differences are the added default-valued `embeddingParameterMap: null`
and `requiresExtendedQueryPlan: 0` fields in native plan JSON embedded in
out-of-range exceptions. Query rewrites, ranges, exception types and
`fullTextSearchTerms` match the existing expectations. Expected baselines and
test exclusions have not been changed; these differences still require explicit
disposition before calling the migration regression-free. Emulator/live-account
coverage remains a separate release requirement.

## Provider selection and packaging

The Client, FaultInjection, tests and source-consuming tools declare explicit
`ProjectReference` entries to the local Direct project. Public builds always use
that source and the QueryPlanInterop NuGet; there is no original Direct package
or native DLL fallback. Reference selection and RID handling remain visible in
each consuming project.
The SDK package bundles the
locally resolved Direct DLL; it does not expose Direct or QueryPlanInterop as
consumer NuGet dependencies. Native packing reads directly from the restored
NuGet asset, independently of the host OS, RID or stale output files.
The original Direct third-party notice is retained in the package under
`ThirdPartyNotices/Microsoft.Azure.Cosmos.Direct.txt`.

Encryption projects built with `SdkProjectRef=true` also reference local Direct
explicitly. Their test and performance consumers explicitly reference HybridRow
in this mode because the SDK's private packaging dependencies do not flow through
source project references. These additions do not apply to internal OSS builds.

The packaging provenance gate requires the locally built Direct assembly and
the QueryPlanInterop native payload. Legacy Direct-package DLLs and CRT payloads
are not packaged or copied by the SDK's consumer targets.

For internal OSS builds, `ProjectRef=True` suppresses both public providers and
the public native package. The existing outer OSS `Directory.Build.targets`
continues to inject original Direct and HybridRow projects. No outer import is
shadowed. The pinned msdata `.net\dirs.proj` traverses the Client project, not this
repository's public solution, so adding Direct to the public solution does not
add it to internal traversal. Building the public Direct project explicitly with
`ProjectRef=True` fails deliberately. Do not use the public solution as an OSS
traversal replacement. Full internal CloudBuild/signing validation is still
required in msdata.

Source-mode Direct consumers use `IsRidAgnostic=false` to retain the application's
RID through MSBuild project-reference traversal. Otherwise netstandard references
drop the RID and select the no-RID native-copy behavior even during a Linux
publish. Publishing tests cover both package and direct Client-project consumers.

## Validation

From the repository root, with .NET SDK 8 or later and PowerShell 7:

```powershell
.\Microsoft.Azure.Cosmos\Direct\Validate-Direct.ps1
.\Microsoft.Azure.Cosmos\Direct\Validate-Direct.ps1 -PackagePath <SDK.nupkg>
```

The first command verifies every imported byte hash and the public project's
evaluated source/resource inventory. The second also checks the packaged managed
and native DLL hashes against their resolved sources, rejects extra native
payloads and checks the consumer dependency boundary. The NuGet-pack pipeline
runs this gate. It does not suppress the existing native query-plan baseline
tests. Upstream source edits require a deliberate manifest update and review;
the validation script never updates hashes automatically.

Before shipping, run the validation script with `-BaselineAssemblyPath` pointing
to the original 3.44.1 DLL and `-ApiCompatPath` pointing to an installed ApiCompat
executable. This adds strict internal API comparison, assembly identity/public
key, friend-grant and resource-name checks. The initial migration passes that
comparison and retains all 79 emitted friend grants. Complete
emulator/live-account and internal release-signing validation separately.
The proposed nightly msdata change-monitoring pipeline is not implemented here.
