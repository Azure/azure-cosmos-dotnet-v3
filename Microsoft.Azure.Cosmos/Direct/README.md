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
templates, notices and the common assembly metadata template. Reconciliation
against the upstream evaluated build remains required before shipping.

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
