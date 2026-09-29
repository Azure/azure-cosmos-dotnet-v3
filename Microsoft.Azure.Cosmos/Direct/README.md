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
