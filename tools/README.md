# Cosmos native-driver (FFI) POC tooling

This folder holds the .NET test bed for the **async FFI native driver** — the
Rust `azure_data_cosmos_driver_native` crate exposed as a C ABI and consumed
from a .NET host.

The flow is two steps: **(1) build the native DLL from Debdatta's Rust PR
([#4515](https://github.com/Azure/azure-sdk-for-rust/pull/4515)), (2) run the
.NET POC against it.** A helper script automates step 1 so you don't have to
remember the cargo invocation.

| Project | What it is |
| --- | --- |
| `Microsoft.Azure.Cosmos.NativeDriverPoc` | The .NET host POC: CRUD/query samples + an F1–F5 smoke harness over the native driver. |

## Prerequisites

- **.NET 8 SDK**
- **Rust toolchain via `rustup`** — the crate pins its channel via
  `rust-toolchain.toml`, so rustup auto-installs the right one on first build.
  Make sure `cargo` is on `PATH` (`$env:Path = "$env:USERPROFILE\.cargo\bin;$env:Path"`).
- **git**
- A local clone of [`azure-sdk-for-rust`](https://github.com/Azure/azure-sdk-for-rust).
- For local testing: the **Azure Cosmos DB Emulator** running (or a real
  Cosmos account).

> Windows needs no extra native deps — the crate uses `rustls`, so OpenSSL is
> **not** required. On macOS/Linux cargo emits `libazurecosmosdriver.{dylib,so}`.

## Step 1 — build the native DLL

```powershell
pwsh Microsoft.Azure.Cosmos.NativeDriverPoc/scripts/build-native-dll.ps1 `
  -RustRepo  <path-to-your-azure-sdk-for-rust-clone> `
  -DropDir   <path-to-drop-the-dll>
```

The script fetches the PR #4515 branch, runs `cargo build --release -p
azure_data_cosmos_driver_native`, and copies `azurecosmosdriver.dll` into
`-DropDir`. Re-run with `-SkipFetch` for a rebuild-only loop, or
`-Configuration debug` for faster local iteration.

`-RustRepo` and `-DropDir` default to `Q:\src\...` paths; pass your own if your
layout differs. **`-DropDir` must match the POC's `DriverNativeArtifactDir`**
(see step 2).

## Step 2 — run the POC

The POC's MSBuild copies the DLL from `DriverNativeArtifactDir` (default
`Q:\src\.poc-artifacts\azurecosmosdriver\`). If you dropped the DLL somewhere
else, pass `-p:DriverNativeArtifactDir=<your-DropDir>\`.

```powershell
cd Microsoft.Azure.Cosmos.NativeDriverPoc

# Against the local emulator (default):
dotnet run                 # F1–F5 smoke harness
dotnet run -- crud         # crud | query | querypk | ryow | hpkcrud | cancel

# Optional sample commands:
dotnet run -- query        # query/querypk/ryow/hpkcrud auto-provision pocdb/items
```

> **Don't commit real account keys.** Use environment variables and never edit
> credentials into source.

## Notes

- `query` currently reports one expected failure on a real/cross-partition
  account: cross-partition `ORDER BY` returns `400/1004
  CrossPartitionQueryNotServable`. That's a known native-driver capability gap,
  not a POC bug.
- The per-project README (`Microsoft.Azure.Cosmos.NativeDriverPoc/README.md`)
  has deeper architecture / interop details.
