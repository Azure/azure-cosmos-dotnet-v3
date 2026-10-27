# CosmosGsiScenario (.NET)

A .NET console port of `scripts/Invoke-CosmosGsiScenario.ps1`. It performs the same end-to-end
Cosmos DB GSI (Global Secondary Index / Materialized Views) scenario, but:

- **Data-plane** operations (document insert, GSI container query) use the **Microsoft.Azure.Cosmos
  .NET SDK** (`CosmosClient`, `Container`, `FeedIterator<T>`), authenticated via `AzureCliCredential`
  (reuses your existing `az login` session). The SDK handles continuation tokens internally.
- **Control-plane** (ARM) operations — account create, RBAC role assignment, database/container
  create, GSI enable, failover-priority-change, multi-master region add/remove, delete — are all
  issued via `az rest` using the Cosmos DB **preview** ARM api-version (`2022-11-15-preview`),
  shelled out from the app (not the `Azure.ResourceManager.CosmosDB` management SDK).

The PowerShell script (`scripts/Invoke-CosmosGsiScenario.ps1`) remains the reference implementation
and is left untouched; this is an independent, equivalent .NET implementation of the same 15-step
scenario.

## Prerequisites

- .NET 8 SDK
- Azure CLI (`az`), logged in (`az login`) to a subscription with Cosmos DB contributor rights
- The signed-in user needs rights to create resource groups / Cosmos DB accounts and role assignments

## Build

```powershell
cd tools/CosmosGsiScenario
dotnet build
```

## Run

```powershell
dotnet run -- [options]
```

| Option | Default | Description |
|---|---|---|
| `--subscription <id>` | `074d02eb-4d74-486a-b299-b262264d1536` | Subscription to use |
| `--resource-group <name>` | `gsi_test` | Resource group (created if missing) |
| `--region1 <region>` | `eastus2` | First region (initial write region for single-master) |
| `--region2 <region>` | `southeastasia` | Second region |
| `--region3 <region>` | *(none)* | Optional third region |
| `--multi-master` | off | Create the account with multiple write locations |
| `--delete-at-end <Yes\|No>` | `No` | Delete the account at the end of the run |
| `--doc-count <n>` | `100` | Number of seed documents to insert |
| `--background-interval-seconds <n>` | `3` | Interval between background inserts |
| `--require-gsi` | off | Fail the run if GSI cannot be enabled |
| `--enable-continuous-backup` | off | Enable continuous backup (PITR) — **required** before GSI can be enabled |
| `--continuous-tier <tier>` | `Continuous7Days` | PITR tier (`Continuous7Days` or `Continuous30Days`) |

### Example: 2-region single-master, mandatory GSI, PITR enabled

```powershell
dotnet run -- --require-gsi --enable-continuous-backup --delete-at-end yes
```

### Example: 3-region multi-master

```powershell
dotnet run -- --region1 eastus2 --region2 southeastasia --region3 westeurope --multi-master --require-gsi --enable-continuous-backup
```

## What it does

1. Logs in using the current `az` CLI session (subscription/tenant selected via `az account set`
   beforehand, or pass `--subscription`).
2. Creates the resource group if it doesn't already exist.
3. Generates account/database/container names as `<alias>-<timestamp>` (alias = your `az account
   show` user name).
4. Creates the Cosmos DB account across the given regions (single-master or multi-master), with
   PITR enabled if requested.
5. Grants the signed-in user the built-in **Cosmos DB Built-in Data Contributor** RBAC role.
6. Creates a database and a `/srcPk`-partitioned source container.
7. Inserts seed documents (`{ id, srcPk, mvPk }`) via the Cosmos DB .NET SDK.
8. Enables GSI (`enableMaterializedViews`) via the documented preview ARM PATCH and verifies it
   took effect.
9. Creates a GSI (materialized view) container over the source container and queries it via the
   .NET SDK's `FeedIterator` (continuation tokens handled automatically).
10. Starts a background task that keeps inserting documents into the source container.
11. **Single-master**: takes the write region offline via `failoverPriorityChange`, confirms the
    write region moved, then restores the original priority order and confirms the original region
    is back online as a read region.
12. **Multi-master**: removes a region (simulated offline), confirms it left `writeLocations`, then
    re-adds it and confirms it rejoined as a write region (multi-master has no read-only role).
13. Optionally deletes the account (`--delete-at-end yes`).

## Notes

- GSI enablement requires continuous backup (PITR) to already be enabled on the account — this is
  a hard prerequisite of the documented preview API, not a subscription feature flag.
- All mutating ARM calls poll for the underlying operation to actually complete (via
  `provisioningState`, or plain existence for resources without one, such as role assignments)
  before proceeding, and retry with backoff on the transient `PreconditionFailed` ("exclusive lock
  in progress") error Cosmos DB's ARM RP can return shortly after a prior mutating call — `az rest`
  does not block for LRO completion the way purpose-built `az cosmosdb ...` commands do.
