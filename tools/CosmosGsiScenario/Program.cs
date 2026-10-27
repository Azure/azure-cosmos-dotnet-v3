// -----------------------------------------------------------------------------------------------
// CosmosGsiScenario
//
// .NET port of scripts/Invoke-CosmosGsiScenario.ps1. Performs the same end-to-end GSI (Global
// Secondary Index / Materialized Views) scenario:
//   1/2. Select subscription (az CLI must already be logged in).
//   3.   Create the resource group if it doesn't exist.
//   4.   Generate account/db/container names as <alias>-<timestamp>.
//   5.   Create a Cosmos DB account across N regions (single-master or multi-master), optionally
//        with continuous backup (PITR) enabled - required before GSI can be enabled.
//   6/7. Create a database and a /srcPk-partitioned source container.
//   8.   Insert seed documents via the Cosmos DB .NET SDK (data plane).
//   9/10.Enable GSI (enableMaterializedViews) on the account and verify it took effect.
//   11/12. Create a GSI (materialized view) container and query it via the .NET SDK.
//   13.  Start a background task that keeps inserting documents into the source container.
//   14/15. Offline/online the write region (single-master: failover-priority-change; multi-master:
//        region add/remove) while the background writes continue, confirming the resulting roles.
//
// IMPORTANT (per explicit instruction): every CONTROL-PLANE (ARM) operation is issued via
// `az rest` using the Cosmos DB preview ARM api-version (2022-11-15-preview), invoked as a child
// process from this .NET app - NOT via the Azure.ResourceManager.CosmosDB management SDK. Only
// DATA-PLANE operations (document insert/query) use the Microsoft.Azure.Cosmos .NET SDK.
// -----------------------------------------------------------------------------------------------

using System.Diagnostics;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;

namespace CosmosGsiScenario;

public static class Program
{
    private const string GsiPreviewApiVersion = "2022-11-15-preview";

    public static async Task<int> Main(string[] args)
    {
        ScenarioOptions options = ScenarioOptions.Parse(args);

        Console.WriteLine($"=== SCENARIO: {options.Regions.Count}-region, {(options.MultiMaster ? "multi-master" : "single-master")} -> regions=[{string.Join(", ", options.Regions)}] ===");

        if (options.RequireGsi && !options.EnableContinuousBackup)
        {
            Console.WriteLine("WARNING: --require-gsi requires continuous backup (PITR) to already be enabled; forcing --enable-continuous-backup on.");
            options = options with { EnableContinuousBackup = true };
        }

        try
        {
            await RunScenarioAsync(options);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FATAL: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task RunScenarioAsync(ScenarioOptions options)
    {
        // -------------------------------------------------------------------------------------
        // 1/2. Login / select subscription (delegates to the already-logged-in az CLI session).
        // -------------------------------------------------------------------------------------
        await AzTool.RunAsync(new[] { "account", "set", "--subscription", options.Subscription });
        string userName = (await AzTool.RunAsync(new[] { "account", "show", "--query", "user.name", "-o", "tsv" })).Trim();
        string alias = new string(userName.Split('@')[0].ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

        // -------------------------------------------------------------------------------------
        // 3. Resource group (plain ARM resource - not Cosmos-specific, so the regular `az group`
        //    commands are used rather than a preview Cosmos DB api-version).
        // -------------------------------------------------------------------------------------
        string rgExists = (await AzTool.RunAsync(new[] { "group", "exists", "-n", options.ResourceGroup })).Trim();
        if (!string.Equals(rgExists, "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Creating resource group '{options.ResourceGroup}'...");
            await AzTool.RunAsync(new[] { "group", "create", "-n", options.ResourceGroup, "-l", options.Regions[0] });
        }

        // -------------------------------------------------------------------------------------
        // 4. Names
        // -------------------------------------------------------------------------------------
        string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string suffix = options.ScenarioTag > 0 ? $"{stamp}{options.ScenarioTag}" : stamp;
        string accountName = $"{alias}-{suffix}";
        string dbName = $"db-{alias}-{suffix}";
        string srcContainerName = $"coll-{alias}-{suffix}";
        string mvContainerName = $"coll-mvPk-{alias}-{suffix}";
        Console.WriteLine($"Account={accountName} Db={dbName} Src={srcContainerName} Mv={mvContainerName}");

        string accountId = $"/subscriptions/{options.Subscription}/resourceGroups/{options.ResourceGroup}/providers/Microsoft.DocumentDB/databaseAccounts/{accountName}";

        // -------------------------------------------------------------------------------------
        // 5. Create the account across N regions (control plane: az rest, preview api-version).
        // -------------------------------------------------------------------------------------
        Console.WriteLine($"=== Creating account '{accountName}' ===");
        await CreateAccountAsync(options, accountId, accountName);

        JsonElement accountShow = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
        string documentEndpoint = accountShow.GetProperty("properties").GetProperty("documentEndpoint").GetString()!.TrimEnd('/');
        Console.WriteLine($"Account created. Endpoint={documentEndpoint}");

        // -------------------------------------------------------------------------------------
        // RBAC grant for AAD data-plane access (Cosmos DB Built-in Data Contributor).
        // -------------------------------------------------------------------------------------
        Console.WriteLine("=== Granting Cosmos DB Built-in Data Contributor ===");
        string principalId = (await AzTool.RunAsync(new[] { "ad", "signed-in-user", "show", "--query", "id", "-o", "tsv" })).Trim();
        string roleAssignmentId = Guid.NewGuid().ToString();
        string roleDefinitionId = $"{accountId}/sqlRoleDefinitions/00000000-0000-0000-0000-000000000002";
        string roleAssignmentResourceId = $"{accountId}/sqlRoleAssignments/{roleAssignmentId}";
        await ArmRest.PutAsync(
            roleAssignmentResourceId,
            GsiPreviewApiVersion,
            new
            {
                properties = new
                {
                    roleDefinitionId,
                    principalId,
                    scope = accountId
                }
            });
        // `az rest` does not wait for async ARM operations to finish (unlike the `az cosmosdb sql
        // role assignment create` CLI command it mirrors), so the account can be left holding an
        // exclusive operation lock briefly after this PUT returns. Role assignments have no
        // `provisioningState`; readability of the resource itself is the completion signal.
        await ArmRest.WaitForExistsAsync(roleAssignmentResourceId, GsiPreviewApiVersion, TimeSpan.FromMinutes(3));
        Console.WriteLine("Waiting for RBAC propagation...");
        await Task.Delay(TimeSpan.FromSeconds(15));

        // -------------------------------------------------------------------------------------
        // 6/7. Database + source container (/srcPk)
        // -------------------------------------------------------------------------------------
        Console.WriteLine($"=== Creating database '{dbName}' and source container '{srcContainerName}' (/srcPk) ===");
        await ArmRest.PutAsync($"{accountId}/sqlDatabases/{dbName}", GsiPreviewApiVersion,
            new { properties = new { resource = new { id = dbName }, options = new { } } });
        await ArmRest.WaitForExistsAsync($"{accountId}/sqlDatabases/{dbName}", GsiPreviewApiVersion, TimeSpan.FromSeconds(90));

        await ArmRest.PutAsync($"{accountId}/sqlDatabases/{dbName}/containers/{srcContainerName}", GsiPreviewApiVersion,
            new
            {
                properties = new
                {
                    resource = new
                    {
                        id = srcContainerName,
                        partitionKey = new { paths = new[] { "/srcPk" }, kind = "Hash" }
                    },
                    options = new { }
                }
            });
        await ArmRest.WaitForExistsAsync($"{accountId}/sqlDatabases/{dbName}/containers/{srcContainerName}", GsiPreviewApiVersion, TimeSpan.FromSeconds(90));
        Console.WriteLine("Source container ready.");

        // -------------------------------------------------------------------------------------
        // Data-plane setup: Cosmos DB .NET SDK client, AAD-authenticated via the same az CLI
        // session used for the control-plane calls above.
        // -------------------------------------------------------------------------------------
        TokenCredential credential = new AzureCliCredential();
        using CosmosClient cosmosClient = new CosmosClient(documentEndpoint, credential, new CosmosClientOptions
        {
            ApplicationName = "CosmosGsiScenario"
        });
        Container srcContainer = cosmosClient.GetContainer(dbName, srcContainerName);

        // RBAC role assignments can take longer than a fixed delay to propagate to the Cosmos DB
        // gateway. Retry a lightweight read until the SDK's AAD-authenticated calls are accepted.
        await WaitForRbacReadyAsync(srcContainer);

        // -------------------------------------------------------------------------------------
        // 8. Insert seed documents (.NET SDK data plane)
        // -------------------------------------------------------------------------------------
        Console.WriteLine($"=== Inserting {options.DocCount} seed documents ===");
        for (int i = 1; i <= options.DocCount; i++)
        {
            string pk = $"pk-{i}";
            await RetryOnForbiddenAsync(() => srcContainer.CreateItemAsync(
                new SeedDocument(Id: $"seed-{i}", SrcPk: pk, MvPk: pk),
                new PartitionKey(pk)));
        }
        Console.WriteLine($"Inserted {options.DocCount} documents.");

        // -------------------------------------------------------------------------------------
        // 9/10. Enable + verify GSI (control plane: az rest PATCH, preview api-version). Requires
        // continuous backup to already be enabled on the account.
        // -------------------------------------------------------------------------------------
        Console.WriteLine($"=== Enabling GSI (enableMaterializedViews) via preview API {GsiPreviewApiVersion} ===");
        bool gsiAvailable = false;
        try
        {
            await ArmRest.PatchAsync(accountId, GsiPreviewApiVersion, new { properties = new { enableMaterializedViews = true } });

            DateTime deadline = DateTime.UtcNow.AddSeconds(180);
            while (DateTime.UtcNow < deadline)
            {
                JsonElement check = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
                if (check.GetProperty("properties").TryGetProperty("enableMaterializedViews", out JsonElement flag) && flag.GetBoolean())
                {
                    gsiAvailable = true;
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
            if (!gsiAvailable)
            {
                throw new InvalidOperationException("enableMaterializedViews not reflected on account after 180s.");
            }
            Console.WriteLine("GSI (enableMaterializedViews) confirmed enabled.");
        }
        catch (Exception ex)
        {
            if (options.RequireGsi)
            {
                throw new InvalidOperationException($"GSI is required (--require-gsi) but could not be enabled: {ex.Message}", ex);
            }
            Console.WriteLine($"WARNING: GSI could not be enabled. Error: {ex.Message}");
        }

        // -------------------------------------------------------------------------------------
        // 11/12. GSI container + query (only if GSI is available)
        // -------------------------------------------------------------------------------------
        if (gsiAvailable)
        {
            Console.WriteLine($"=== Creating GSI container '{mvContainerName}' ===");
            await ArmRest.PutAsync($"{accountId}/sqlDatabases/{dbName}/containers/{mvContainerName}", GsiPreviewApiVersion,
                new
                {
                    properties = new
                    {
                        resource = new
                        {
                            id = mvContainerName,
                            partitionKey = new { paths = new[] { "/mvPk" }, kind = "Hash" },
                            materializedViewDefinition = new
                            {
                                sourceCollectionId = srcContainerName,
                                definition = "SELECT * FROM c"
                            }
                        },
                        options = new { autoscaleSettings = new { maxThroughput = 4000 } }
                    }
                });
            await ArmRest.WaitForExistsAsync($"{accountId}/sqlDatabases/{dbName}/containers/{mvContainerName}", GsiPreviewApiVersion, TimeSpan.FromSeconds(180));
            Console.WriteLine("GSI container ready; waiting for change-feed sync...");
            await Task.Delay(TimeSpan.FromSeconds(30));

            Console.WriteLine("=== Querying GSI container ===");
            Container mvContainer = cosmosClient.GetContainer(dbName, mvContainerName);
            // The .NET SDK's FeedIterator handles server-side continuation tokens internally -
            // no manual x-ms-continuation handling is needed (unlike the raw-REST PowerShell version).
            QueryDefinition query = new QueryDefinition("SELECT * FROM c");
            int totalDocs = 0;
            using (FeedIterator<Dictionary<string, object>> iterator = mvContainer.GetItemQueryIterator<Dictionary<string, object>>(
                query,
                requestOptions: new QueryRequestOptions { MaxItemCount = 10 }))
            {
                while (iterator.HasMoreResults)
                {
                    FeedResponse<Dictionary<string, object>> page = await iterator.ReadNextAsync();
                    totalDocs += page.Count;
                }
            }
            Console.WriteLine($"GSI container query returned {totalDocs} document(s) total (paged via SDK FeedIterator).");
        }
        else
        {
            Console.WriteLine("Skipping steps 11/12 (GSI container + query) - GSI not enabled for this account.");
        }

        // -------------------------------------------------------------------------------------
        // 13. Background insert job
        // -------------------------------------------------------------------------------------
        Console.WriteLine("=== Starting background insert job ===");
        using CancellationTokenSource backgroundCts = new CancellationTokenSource();
        Task backgroundTask = Task.Run(() => BackgroundInsertLoopAsync(srcContainer, options.BackgroundInsertIntervalSeconds, backgroundCts.Token));

        // -------------------------------------------------------------------------------------
        // 14/15. Offline a region while background writes continue, then confirm + restore
        // -------------------------------------------------------------------------------------
        if (!options.MultiMaster)
        {
            await RunSingleMasterFailoverAsync(options, accountId, accountName);
        }
        else
        {
            await RunMultiMasterOfflineOnlineAsync(options, accountId, accountName);
        }

        backgroundCts.Cancel();
        try { await backgroundTask; } catch (OperationCanceledException) { }
        Console.WriteLine("Background insert job stopped.");

        // -------------------------------------------------------------------------------------
        // Optional cleanup
        // -------------------------------------------------------------------------------------
        if (string.Equals(options.DeleteAtEnd, "Yes", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"=== Deleting account '{accountName}' (--delete-at-end Yes) ===");
            await ArmRest.DeleteAsync(accountId, GsiPreviewApiVersion);
            Console.WriteLine("Delete requested.");
        }
        else
        {
            Console.WriteLine($"Account '{accountName}' left running (--delete-at-end No).");
        }

        Console.WriteLine("=== SCENARIO COMPLETE ===");
    }

    /// <summary>
    /// RBAC (data-plane AAD role) assignments can take longer than a fixed delay to propagate.
    /// Retry a lightweight metadata read until the SDK's AAD-authenticated calls are accepted,
    /// rather than failing the whole run on a transient 403.
    /// </summary>
    private static async Task WaitForRbacReadyAsync(Container container, int maxAttempts = 24, int delaySeconds = 15)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await container.ReadContainerAsync();
                return;
            }
            catch (CosmosException ex) when (attempt < maxAttempts && (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.SubStatusCode == 5301))
            {
                Console.WriteLine($"  (RBAC not yet propagated, retrying in {delaySeconds}s... attempt {attempt}/{maxAttempts})");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    /// <summary>
    /// Wraps a data-plane call with a short retry for the same transient RBAC-propagation 403
    /// that can surface on the very first calls after granting a role assignment.
    /// </summary>
    private static async Task RetryOnForbiddenAsync(Func<Task> action, int maxAttempts = 6, int delaySeconds = 15)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (CosmosException ex) when (attempt < maxAttempts && (ex.StatusCode == System.Net.HttpStatusCode.Forbidden || ex.SubStatusCode == 5301))
            {
                Console.WriteLine($"  (RBAC not yet propagated, retrying in {delaySeconds}s... attempt {attempt}/{maxAttempts})");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
    }

    private static async Task CreateAccountAsync(ScenarioOptions options, string accountId, string accountName)
    {
        List<object> locations = new List<object>();
        for (int i = 0; i < options.Regions.Count; i++)
        {
            locations.Add(new { locationName = options.Regions[i], failoverPriority = i, isZoneRedundant = false });
        }

        var properties = new Dictionary<string, object?>
        {
            ["databaseAccountOfferType"] = "Standard",
            ["locations"] = locations,
            ["consistencyPolicy"] = new { defaultConsistencyLevel = "Session" },
            ["disableLocalAuth"] = true,
            ["enableMultipleWriteLocations"] = options.MultiMaster
        };
        if (!options.MultiMaster)
        {
            properties["enableAutomaticFailover"] = false;
        }
        if (options.EnableContinuousBackup)
        {
            properties["backupPolicy"] = new
            {
                type = "Continuous",
                continuousModeProperties = new { tier = options.ContinuousTier }
            };
            Console.WriteLine($"PITR: continuous backup enabled ({options.ContinuousTier}).");
        }

        var body = new
        {
            location = options.Regions[0],
            kind = "GlobalDocumentDB",
            properties
        };

        await ArmRest.PutAsync(accountId, GsiPreviewApiVersion, body);
        await ArmRest.WaitForProvisioningSucceededAsync(accountId, GsiPreviewApiVersion, TimeSpan.FromMinutes(10));
    }

    private static async Task BackgroundInsertLoopAsync(Container container, int intervalSeconds, CancellationToken cancellationToken)
    {
        int i = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            i++;
            string pk = $"bg-pk-{i}";
            try
            {
                await container.CreateItemAsync(
                    new SeedDocument(Id: $"bg-{Guid.NewGuid()}", SrcPk: pk, MvPk: pk),
                    new PartitionKey(pk),
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                Console.WriteLine($"  [background insert #{i}] failed (continuing): {ex.Message}");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private static async Task RunSingleMasterFailoverAsync(ScenarioOptions options, string accountId, string accountName)
    {
        string writeRegion = options.Regions[0];
        string nextRegion = options.Regions[1];
        Console.WriteLine($"=== Single-master: taking write region '{writeRegion}' offline via failover-priority-change ===");

        List<object> offlinePolicies = new List<object> { new { locationName = nextRegion, failoverPriority = 0 }, new { locationName = writeRegion, failoverPriority = 1 } };
        if (options.Regions.Count == 3)
        {
            offlinePolicies.Add(new { locationName = options.Regions[2], failoverPriority = 2 });
        }
        await ArmRest.PostAsync($"{accountId}/failoverPriorityChange", GsiPreviewApiVersion, new { failoverPolicies = offlinePolicies });
        // `az rest` (unlike `az cosmosdb failover-priority-change`) does not wait for the LRO to
        // finish, so poll provisioningState before trusting a "settled" read of writeLocations.
        await ArmRest.WaitForProvisioningSucceededAsync(accountId, GsiPreviewApiVersion, TimeSpan.FromMinutes(5));
        Console.WriteLine("Waiting for failover to settle...");
        await Task.Delay(TimeSpan.FromSeconds(90));

        JsonElement postFailover = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
        string currentWrite = GetTopWriteRegionSlug(postFailover, accountName);
        Console.WriteLine($"Current write region slug: {currentWrite}");
        if (string.Equals(currentWrite, nextRegion, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"CONFIRMED: write region moved from {writeRegion} to {nextRegion}.");
        }
        else
        {
            Console.WriteLine($"WARNING: expected write region '{nextRegion}' but found '{currentWrite}'.");
        }

        Console.WriteLine($"=== Restoring original priority order (bringing '{writeRegion}' back online as a read region) ===");
        List<object> restorePolicies = new List<object> { new { locationName = writeRegion, failoverPriority = 0 }, new { locationName = nextRegion, failoverPriority = 1 } };
        if (options.Regions.Count == 3)
        {
            restorePolicies.Add(new { locationName = options.Regions[2], failoverPriority = 2 });
        }
        await ArmRest.PostAsync($"{accountId}/failoverPriorityChange", GsiPreviewApiVersion, new { failoverPolicies = restorePolicies });
        await ArmRest.WaitForProvisioningSucceededAsync(accountId, GsiPreviewApiVersion, TimeSpan.FromMinutes(5));
        await Task.Delay(TimeSpan.FromSeconds(90));

        JsonElement final = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
        string finalWrite = GetTopWriteRegionSlug(final, accountName);
        bool isRead = GetRegionSlugs(final, "readLocations", accountName).Contains(writeRegion, StringComparer.OrdinalIgnoreCase);
        if (string.Equals(finalWrite, writeRegion, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"CONFIRMED: '{writeRegion}' is the write region again (back to original).");
        }
        else
        {
            Console.WriteLine($"WARNING: expected '{writeRegion}' to be write region again but found '{finalWrite}'.");
        }
        if (isRead)
        {
            Console.WriteLine($"CONFIRMED: '{writeRegion}' present among read locations.");
        }
    }

    private static async Task RunMultiMasterOfflineOnlineAsync(ScenarioOptions options, string accountId, string accountName)
    {
        string regionToOffline = options.Regions[0];
        List<string> remainingRegions = options.Regions.Where(r => !string.Equals(r, regionToOffline, StringComparison.OrdinalIgnoreCase)).ToList();

        Console.WriteLine($"=== Multi-master: removing region '{regionToOffline}' from account (simulated offline) ===");
        List<object> withoutRegion = remainingRegions.Select((r, idx) => (object)new { locationName = r, failoverPriority = idx, isZoneRedundant = false }).ToList();
        await ArmRest.PatchAsync(accountId, GsiPreviewApiVersion, new { properties = new { locations = withoutRegion } });
        await ArmRest.WaitForProvisioningSucceededAsync(accountId, GsiPreviewApiVersion, TimeSpan.FromMinutes(10));
        Console.WriteLine("Waiting for region removal to settle (multi-master region changes can take several minutes)...");
        await Task.Delay(TimeSpan.FromMinutes(5));

        JsonElement afterRemoval = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
        List<string> writeRegionsAfterRemoval = GetRegionSlugs(afterRemoval, "writeLocations", accountName);
        if (!writeRegionsAfterRemoval.Contains(regionToOffline, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"CONFIRMED: '{regionToOffline}' removed from write locations: [{string.Join(", ", writeRegionsAfterRemoval)}].");
        }
        else
        {
            Console.WriteLine($"WARNING: '{regionToOffline}' still present in write locations: [{string.Join(", ", writeRegionsAfterRemoval)}].");
        }

        Console.WriteLine($"=== Multi-master: re-adding region '{regionToOffline}' (comes back as a write region - there is no read-only role in multi-master) ===");
        List<object> withRegion = options.Regions.Select((r, idx) => (object)new { locationName = r, failoverPriority = idx, isZoneRedundant = false }).ToList();
        await ArmRest.PatchAsync(accountId, GsiPreviewApiVersion, new { properties = new { locations = withRegion } });
        await ArmRest.WaitForProvisioningSucceededAsync(accountId, GsiPreviewApiVersion, TimeSpan.FromMinutes(10));
        Console.WriteLine("Waiting for region re-add to settle...");
        await Task.Delay(TimeSpan.FromMinutes(5));

        JsonElement final = await ArmRest.GetAsync(accountId, GsiPreviewApiVersion);
        List<string> writeRegionsFinal = GetRegionSlugs(final, "writeLocations", accountName);
        if (writeRegionsFinal.Contains(regionToOffline, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"CONFIRMED: '{regionToOffline}' is a write region again (expected - multi-master has no read-only role).");
        }
        else
        {
            Console.WriteLine($"WARNING: expected '{regionToOffline}' back among write locations but found [{string.Join(", ", writeRegionsFinal)}].");
        }
    }

    private static string GetTopWriteRegionSlug(JsonElement account, string accountName)
    {
        List<(string Slug, int Priority)> writeLocations = account.GetProperty("properties").GetProperty("writeLocations")
            .EnumerateArray()
            .Select(loc => (Slug: GetRegionSlug(loc, accountName), Priority: loc.GetProperty("failoverPriority").GetInt32()))
            .OrderBy(t => t.Priority)
            .ToList();
        return writeLocations.First().Slug;
    }

    private static List<string> GetRegionSlugs(JsonElement account, string property, string accountName)
    {
        return account.GetProperty("properties").GetProperty(property)
            .EnumerateArray()
            .Select(loc => GetRegionSlug(loc, accountName))
            .ToList();
    }

    private static string GetRegionSlug(JsonElement location, string accountName)
    {
        string id = location.GetProperty("id").GetString() ?? string.Empty;
        string prefix = $"{accountName}-";
        return id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? id[prefix.Length..] : id;
    }
}

public sealed record SeedDocument(
    [property: Newtonsoft.Json.JsonProperty("id")] string Id,
    [property: Newtonsoft.Json.JsonProperty("srcPk")] string SrcPk,
    [property: Newtonsoft.Json.JsonProperty("mvPk")] string MvPk);

public sealed record ScenarioOptions
{
    public string Subscription { get; init; } = "074d02eb-4d74-486a-b299-b262264d1536";
    public string ResourceGroup { get; init; } = "gsi_test";
    public List<string> Regions { get; init; } = new() { "eastus2", "southeastasia" };
    public bool MultiMaster { get; init; }
    public string DeleteAtEnd { get; init; } = "No";
    public int DocCount { get; init; } = 100;
    public int BackgroundInsertIntervalSeconds { get; init; } = 3;
    public int ScenarioTag { get; init; }
    public bool RequireGsi { get; init; }
    public bool EnableContinuousBackup { get; init; }
    public string ContinuousTier { get; init; } = "Continuous7Days";

    public static ScenarioOptions Parse(string[] args)
    {
        ScenarioOptions options = new ScenarioOptions();
        string? region1 = null;
        string? region2 = null;
        string? region3 = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"Missing value for {arg}");
            switch (arg.ToLowerInvariant())
            {
                case "--subscription": options = options with { Subscription = Next() }; break;
                case "--resource-group": options = options with { ResourceGroup = Next() }; break;
                case "--region1": region1 = Next(); break;
                case "--region2": region2 = Next(); break;
                case "--region3": region3 = Next(); break;
                case "--multi-master": options = options with { MultiMaster = true }; break;
                case "--delete-at-end": options = options with { DeleteAtEnd = Next() }; break;
                case "--doc-count": options = options with { DocCount = int.Parse(Next()) }; break;
                case "--background-interval-seconds": options = options with { BackgroundInsertIntervalSeconds = int.Parse(Next()) }; break;
                case "--scenario-tag": options = options with { ScenarioTag = int.Parse(Next()) }; break;
                case "--require-gsi": options = options with { RequireGsi = true }; break;
                case "--enable-continuous-backup": options = options with { EnableContinuousBackup = true }; break;
                case "--continuous-tier": options = options with { ContinuousTier = Next() }; break;
                default: throw new ArgumentException($"Unknown argument: {arg}");
            }
        }

        List<string> regions = new List<string> { region1 ?? "eastus2", region2 ?? "southeastasia" };
        if (region3 is not null) { regions.Add(region3); }
        options = options with { Regions = regions };
        if (options.Regions.Count is < 2 or > 3)
        {
            throw new ArgumentException("Regions must contain 2 or 3 entries.");
        }
        return options;
    }
}

/// <summary>
/// Thin wrapper for shelling out to the Azure CLI (`az`). Used for login/subscription context and
/// for the couple of non-Cosmos-specific calls (resource group existence/creation, AAD lookups)
/// that don't need a Cosmos DB preview api-version.
/// </summary>
internal static class AzTool
{
    public static async Task<string> RunAsync(IReadOnlyList<string> args)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add("az");
        foreach (string a in args) { psi.ArgumentList.Add(a); }

        using Process process = Process.Start(psi)!;
        string stdOut = await process.StandardOutput.ReadToEndAsync();
        string stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"az {string.Join(' ', args)} failed (exit {process.ExitCode}):\n{stdOut}\n{stdErr}");
        }
        return stdOut;
    }
}

/// <summary>
/// All Cosmos DB CONTROL-PLANE (ARM) operations for this scenario go through this class, which
/// shells out to `az rest` using the Cosmos DB preview api-version, per explicit instruction -
/// rather than using the Azure.ResourceManager.CosmosDB management SDK.
/// </summary>
internal static class ArmRest
{
    private const string ManagementBaseUri = "https://management.azure.com";

    public static async Task<JsonElement> GetAsync(string resourceId, string apiVersion)
    {
        string uri = BuildUri(resourceId, apiVersion);
        string output = await RunWithLockRetryAsync(new[] { "rest", "--method", "get", "--uri", uri });
        return JsonDocument.Parse(output).RootElement.Clone();
    }

    public static async Task PutAsync(string resourceId, string apiVersion, object body)
    {
        string uri = BuildUri(resourceId, apiVersion);
        string bodyJson = JsonSerializer.Serialize(body);
        await RunWithLockRetryAsync(new[] { "rest", "--method", "put", "--uri", uri, "--body", bodyJson });
    }

    public static async Task PatchAsync(string resourceId, string apiVersion, object body)
    {
        string uri = BuildUri(resourceId, apiVersion);
        string bodyJson = JsonSerializer.Serialize(body);
        await RunWithLockRetryAsync(new[] { "rest", "--method", "patch", "--uri", uri, "--body", bodyJson });
    }

    public static async Task PostAsync(string resourceId, string apiVersion, object body)
    {
        string uri = BuildUri(resourceId, apiVersion);
        string bodyJson = JsonSerializer.Serialize(body);
        await RunWithLockRetryAsync(new[] { "rest", "--method", "post", "--uri", uri, "--body", bodyJson });
    }

    public static async Task DeleteAsync(string resourceId, string apiVersion)
    {
        string uri = BuildUri(resourceId, apiVersion);
        await RunWithLockRetryAsync(new[] { "rest", "--method", "delete", "--uri", uri });
    }

    /// <summary>
    /// The Cosmos DB ARM control plane briefly holds an exclusive per-account operation lock after
    /// any mutating call (create/update/container ops). A subsequent mutating call issued shortly
    /// after can fail with PreconditionFailed ("there is already an operation in progress which
    /// requires exclusive lock"). Retry with backoff instead of failing immediately.
    /// </summary>
    private static async Task<string> RunWithLockRetryAsync(IReadOnlyList<string> args, int maxAttempts = 40, int delaySeconds = 15)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await AzTool.RunAsync(args);
            }
            catch (InvalidOperationException ex) when (attempt < maxAttempts && ex.Message.Contains("PreconditionFailed", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  (exclusive lock in progress, retrying in {delaySeconds}s... attempt {attempt}/{maxAttempts})");
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }
        }
        return await AzTool.RunAsync(args);
    }


    public static async Task WaitForExistsAsync(string resourceId, string apiVersion, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await GetAsync(resourceId, apiVersion);
                return;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }
        throw new TimeoutException($"Timed out waiting for '{resourceId}' to be provisioned.");
    }

    public static async Task WaitForProvisioningSucceededAsync(string resourceId, string apiVersion, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            JsonElement show;
            try
            {
                show = await GetAsync(resourceId, apiVersion);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
            {
                // Brief eventual-consistency lag right after a PUT: the resource may not be
                // readable for a moment. Retry instead of failing.
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }
            if (show.GetProperty("properties").TryGetProperty("provisioningState", out JsonElement state))
            {
                string? stateValue = state.GetString();
                if (string.Equals(stateValue, "Succeeded", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (string.Equals(stateValue, "Failed", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Provisioning of '{resourceId}' failed.");
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(15));
        }
        throw new TimeoutException($"Timed out waiting for '{resourceId}' provisioning to succeed.");
    }

    private static string BuildUri(string resourceId, string apiVersion) => $"{ManagementBaseUri}{resourceId}?api-version={apiVersion}";
}
