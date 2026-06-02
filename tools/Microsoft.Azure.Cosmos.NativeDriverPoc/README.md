# Microsoft.Azure.Cosmos.NativeDriverPoc (V2, spec-aligned)

This is the **V2** .NET host for the Cosmos async-FFI work. It targets the
production spec drafted in
[Azure/azure-sdk-for-rust#4461](https://github.com/Azure/azure-sdk-for-rust/pull/4461)
— the `azure_data_cosmos_driver_native` crate, which produces
`azurecosmosdriver.{dll,so,dylib}`.

The sibling project [`Microsoft.Azure.Cosmos.NativeAsyncPoc`](../Microsoft.Azure.Cosmos.NativeAsyncPoc/)
(V1) remains the validated baseline against the original 14-function
hand-rolled cdylib. V1 stays as the evidence behind the verdict doc;
V2 is the path forward once the production crate lands.

## Why two projects side-by-side

| | V1 (NativeAsyncPoc) | V2 (NativeDriverPoc) |
|---|---|---|
| Targets | hand-rolled `cosmos_async_poc.dll` | spec-aligned `azurecosmosdriver.dll` |
| Function count | 14 P/Invokes | ~50 P/Invokes |
| Error model | flat `CosmosStatus` enum | two-tier `CosmosErrorCode` + rich `cosmos_error_t` |
| Completion | 3 out-params (`user_data`, `status`, `response`) | 1 opaque `cosmos_completion_t*` + accessor family |
| Cancellation | `cosmos_cancel(opHandle)` | `cosmos_operation_handle_cancel(opHandle)` + cooperative |
| Status | **PROVEN** — F1-F4 all green on emulator | **READY** — compiles green; runs the moment the DLL lands |

Function-for-function diff is intentional: reviewers can place the two
`NativeMethods.cs` files side by side and audit every spec interpretation
against the V1 ground truth.

## Build behavior

```powershell
# Build without the DLL — succeeds with one warning.
dotnet build .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\

# Build with the DLL — bundles it into the output directory.
$env:DriverNativeArtifactDir = "Q:\src\.poc-artifacts\azurecosmosdriver\"
dotnet build .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\

# CI knob — escalate missing DLL to an error.
dotnet build .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\ `
  -p:RequireDriverNativeArtifact=true
```

The MSBuild target `ReportDriverNativeArtifactStatus` checks for the DLL
at `$(DriverNativeArtifactDir)\azurecosmosdriver.dll` and either:
* copies it next to the .exe and prints a confirmation, **or**
* emits a warning explaining how to produce it (default), **or**
* fails the build (when `RequireDriverNativeArtifact=true`).

The executable runs a preflight check on `Main` entry; if the DLL is
absent it exits 2 with the same remediation steps printed to stderr —
friendlier than a raw `DllNotFoundException` at the first call.

## When the DLL arrives — drop-in steps

1. Pull the feature branch behind PR #4452 in `azure-sdk-for-rust`.
2. `cargo build -p azure_data_cosmos_driver_native --release`
3. Copy `target\release\azurecosmosdriver.dll` to
   `Q:\src\.poc-artifacts\azurecosmosdriver\` (or set
   `DriverNativeArtifactDir` to wherever it lives).
4. `dotnet run --project .\tools\Microsoft.Azure.Cosmos.NativeDriverPoc\`
5. F1-F5 should pass against a running emulator.

No .NET code changes are anticipated unless the spec moves between now
and the merge.

## F-checks

| | Check | New vs V1? |
|---|---|---|
| F1 | Single read returns 200 + seeded body marker | same shape |
| F2 | 1000 submits, average < 100µs (non-blocking submit) | scaled up |
| F3 | 1000 concurrent reads on one pump complete in <5s | scaled up |
| F4 | CancellationToken → `cosmos_operation_handle_cancel` honored on 100 trials | reuses V1 idea, larger sample |
| F5 | Read non-existent item surfaces `CosmosNativeException(IsNotFound=true, HttpStatusCode=404)` | **NEW** — validates spec §6.2 "404 surfaces as ERROR not OK" |

## Spec ambiguities flagged

These are open questions discovered while writing the bindings; each
deserves a comment on PR #4461 before the spec is signed off.

* **`cosmos_error_t *out_error` is opaque + owned by caller.** Multiple
  entry points (`cosmos_runtime_builder_build`,
  `cosmos_account_ref_with_master_key`,
  `cosmos_driver_get_or_create_blocking`) declare a trailing
  `cosmos_error_t *out_error` (single pointer). Because `cosmos_error_t`
  is opaque and freed via `cosmos_error_free`, the only sane
  interpretation is `cosmos_error_t **out_error` — wrapper allocates,
  caller frees. V2 binds it as `out IntPtr` on the assumption that the
  spec text means double-pointer.
* **`cosmos_bytes_view_t` returned by value.** Spec §3.3 publishes the
  layout and uses pass-by-value for both inputs and outputs. On Windows
  x64 with MS ABI, 16-byte aggregate return goes via hidden pointer;
  cbindgen + Rust `extern "C"` should handle that, and so should the
  .NET P/Invoke marshaller, but this is unverified until the DLL ships
  and F1 actually runs.
* **`cosmos_cq_options_t` capacity defaults.** Spec lists
  `CapacityHint`, `MaxCapacity`, `IncludeErrorDetails` but doesn't pin
  default values when zero is passed. V2 defaults to 1024 / 0
  (unbounded) / true for safety; would prefer the spec be explicit.
* **`cosmos_completion_was_cancel_requested` access pattern.** Spec
  says this predicate is true on the completion record after cancel
  was requested, regardless of outcome. The current pump consumes
  outcome and frees the completion in one breath; surfacing this
  predicate to user code would require either a richer
  `CosmosNativeResponse` (with `WasCancelRequested`) or a callback hook.
  V2 leaves it on the table for the post-DLL iteration.

## File map

| File | What it is |
|---|---|
| `Microsoft.Azure.Cosmos.NativeDriverPoc.csproj` | Build def + MSBuild target that warn-or-bundles the native DLL. |
| `NativeMethods.cs` | Raw P/Invoke surface (~50 functions, every signature tagged with its spec section). |
| `CosmosNativeException.cs` | Rich-error wrapper + materialized `CosmosNativeResponse` (both copy out of native memory in the ctor). |
| `CompletionQueueLoop.cs` | One pump thread per CQ; spec §3.1.3 NULL-result disambiguation; outcome-dispatch onto TCS. |
| `NativeCosmosClient.cs` | Owns the full object graph (runtime / account / db / container / pk / driver / cq); CRUD `*Async` methods. |
| `Program.cs` | F1-F5 driver against the emulator. |

## Branch / commit context

* Branch: `users/ananth/poc-native-async-spike` in worktree
  `Q:\src\azure-cosmos-dotnet-v3\worktrees\poc-async-ffi`.
* V1 commits: `5486eb7b4` (V1 .NET host), `438470960` (V1 .sln).
* V2 commit: see the commit that adds this README.
