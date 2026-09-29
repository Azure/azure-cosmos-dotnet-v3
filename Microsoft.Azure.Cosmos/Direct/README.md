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
**1.0.2**, acquired exclusively through NuGet. Only its Windows x64 DLL is used,
renamed to `Microsoft.Azure.Cosmos.ServiceInterop.dll` for the existing P/Invoke
contract. No `.lib`, `.pdb`, CRTCompat or Visual C++ runtime DLL is copied from
CosmosDB. Existing platform-selection logic is unchanged: supported Windows
processes use native planning; other platforms use the existing service query-plan
path. Copying the x64 asset for a Windows RID does not add ARM64 or x86 native
support.

Managed dependencies remain Newtonsoft.Json, ConfigurationManager,
DiagnosticSource, System.Memory and Tasks.Extensions. The Client package also
continues to bundle the existing NuGet-provided Core and HybridRow assemblies.

## Known native compatibility issue

Do not ship this migration as regression-free. With native package 1.0.2,
`QueryPlanBaselineTests` fails `NonValueAggregates`, `GroupBy`, `Negative`,
`PointRange`, `Top`, `OffsetLimit` and `Spatial`. Spatial and some point-range
queries fail with `NotImplementedException` instead of producing a valid plan.
The isolated original Direct 3.44.1 package passes the corresponding tests.
The native-package owner must resolve or explicitly disposition these differences
before release. Expected baselines and test exclusions have not been changed.

## Provider selection and packaging

`DirectReference.props` is shared by the Client, FaultInjection, tests and tools.
Public builds default to `UseDirectProject=true`. The SDK package bundles the
locally resolved Direct DLL; it does not expose Direct or QueryPlanInterop as
consumer NuGet dependencies. Native packing reads directly from the restored
NuGet asset, independently of the host OS, RID or stale output files.
The original Direct third-party notice is retained in the package under
`ThirdPartyNotices/Microsoft.Azure.Cosmos.Direct.txt`.

`UseDirectProject=false` is a temporary rollback to the original Direct 3.44.1
NuGet package. Use a separate `--artifacts-path` for A/B comparisons so native
outputs from the two providers cannot contaminate one another. The source-mode
packaging provenance gate intentionally requires the new payload.

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
