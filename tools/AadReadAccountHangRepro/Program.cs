// ---------------------------------------------------------------------------
// ReadAccountAsync hang repro / bisection harness
// Seon work item: repro-readaccountasync-hang-with-aad-after-pr-5999
//
// Purpose
//   Reproduce the internal team's report ("ReadAccountAsync hangs, AAD auth,
//   healthy account") against the local PREVIEW NuGet built from PR #5999
//   ("Metadata Hedging"), and AUTO-BISECT the two features that PR #5999 and
//   its sibling PR #5970 (ThinClient) turn on by default in the preview package:
//
//       AZURE_COSMOS_METADATA_HEDGING_ENABLED   (PR #5999, default ON in preview)
//       AZURE_COSMOS_THIN_CLIENT_ENABLED        (PR #5970, default ON in preview)
//
//   Each run builds the client exactly like the customer snippet (Direct mode,
//   AAD TokenCredential, CrossRegionHedgingStrategy, plus the reflection-set
//   internal properties incl. ReadConsistencyStrategy=LastCommittedSingleWriteRegion),
//   then calls ReadAccountAsync() under a watchdog. On a hang it prints the PID,
//   dotnet-stack/dotnet-dump instructions, and the last SDK trace events captured
//   from the in-proc EventListener — i.e. it captures exactly the data needed to
//   pin the root cause.
//
// Usage
//   set COSMOS_ACCOUNT_ENDPOINT=https://<account>.documents.azure.com:443/
//   # AAD: uses DefaultAzureCredential (az login / managed identity / env), OR set
//   #      COSMOS_TENANT_ID + COSMOS_CLIENT_ID + COSMOS_CLIENT_CERT_PATH for a cert.
//   dotnet run                      # runs the full bisection matrix, stops on first hang
//   dotnet run -- --combo baseline-unified   # run ONE combo (used by run-matrix.ps1)
//   dotnet run -- --timeout 60      # watchdog seconds (default 30)
// ---------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.FaultInjection;

namespace ReadAccountHangRepro
{
    internal static class Program
    {
        // Approximate values for the customer constants not shown in the snippet.
        // Adjust to the team's real values if known; they do not change the bisection outcome.
        private const string ApplicationName = "ReadAccountHangRepro";
        private const int MaxRetryAttemptsOnThrottledRequests = 9;
        private const int MaxRetryWaitTimeInSeconds = 30;
        private const int OpenTcpConnectionTimeoutInSec = 5;
        private const int RequestTimeoutInSeconds = 10;
        private static readonly TimeSpan HedgingThreshold = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan HedgingThresholdStep = TimeSpan.FromMilliseconds(100);

        private static readonly SdkEventCollector EventCollector = new SdkEventCollector();

        private static bool DoContainerRead;
        private static string DbName = "hangrepro";
        private static string ContainerName = "probe";

        private static async Task<int> Main(string[] args)
        {
            string? connectionString = Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING");
            string? endpoint = Environment.GetEnvironmentVariable("COSMOS_ACCOUNT_ENDPOINT");
            if (string.IsNullOrWhiteSpace(endpoint) && args.Length > 0 && !args[0].StartsWith("--"))
            {
                endpoint = args[0];
            }

            // Derive the endpoint from the connection string when not given explicitly.
            if (string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(connectionString))
            {
                foreach (string part in connectionString.Split(';'))
                {
                    if (part.StartsWith("AccountEndpoint=", StringComparison.OrdinalIgnoreCase))
                    {
                        endpoint = part.Substring("AccountEndpoint=".Length).Trim();
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Console.Error.WriteLine("ERROR: set COSMOS_ACCOUNT_ENDPOINT or COSMOS_CONNECTION_STRING (or pass the endpoint as the first arg).");
                return 2;
            }

            int timeoutSeconds = GetIntArg(args, "--timeout", 30);
            string? singleCombo = GetStrArg(args, "--combo");
            DoContainerRead = args.Contains("--containerread");
            DbName = Environment.GetEnvironmentVariable("COSMOS_DB") ?? DbName;
            ContainerName = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? ContainerName;

            // Auth mode: 'aad' (faithful to the customer report) or 'key' (control run using the
            // connection string). Default: aad when we can, otherwise key.
            string authMode = GetStrArg(args, "--auth")
                ?? (string.IsNullOrWhiteSpace(connectionString) ? "aad" : "aad");
            authMode = authMode.ToLowerInvariant();
            if (authMode == "key" && string.IsNullOrWhiteSpace(connectionString))
            {
                Console.Error.WriteLine("ERROR: --auth key requires COSMOS_CONNECTION_STRING.");
                return 2;
            }

            Console.WriteLine($"PID = {Environment.ProcessId}   (on hang: dotnet-stack report -p {Environment.ProcessId})");
            Console.WriteLine($"Endpoint = {endpoint}");
            Console.WriteLine($"Auth mode = {authMode}");
            Console.WriteLine($"Container read = {(DoContainerRead ? $"ON (db='{DbName}', container='{ContainerName}')" : "off")}");
            Console.WriteLine($"Watchdog timeout = {timeoutSeconds}s");
            Console.WriteLine();

            TokenCredential? credential = authMode == "aad" ? BuildCredential() : null;

            // --tokenprobe: wrap the credential to record the TokenRequestContext the SDK passes on
            // every token acquisition. PR #5549 (CAE) injects a non-empty Claims (cp1) on EVERY
            // acquire, which makes Azure.Identity/MSAL bypass its token cache and go live to ESTS each
            // time — the root cause of the AAD ReadAccountAsync hang between 3.61.0 and main.
            if (args.Contains("--tokenprobe"))
            {
                return await RunTokenProbeAsync(endpoint!, credential, connectionString, authMode, timeoutSeconds);
            }

            // --faultinject: force a metadata hedge to actually FIRE by delaying the primary region's
            // Collection Read 6s (> the 1.5s hedge threshold), then confirm #5999's hedge machinery
            // completes (does not hang) and a secondary region wins. This is the definitive test of
            // the new code path — a healthy account never fires a hedge on its own.
            if (args.Contains("--faultinject"))
            {
                return await RunFaultInjectionAsync(endpoint!, credential, connectionString, authMode, timeoutSeconds);
            }

            List<Combo> combos = BuildMatrix();
            if (singleCombo != null)
            {
                combos = combos.Where(c => c.Id.Equals(singleCombo, StringComparison.OrdinalIgnoreCase)).ToList();
                if (combos.Count == 0)
                {
                    Console.Error.WriteLine($"ERROR: unknown combo '{singleCombo}'. Known: {string.Join(", ", BuildMatrix().Select(c => c.Id))}");
                    return 2;
                }
            }

            var results = new List<ComboResult>();
            foreach (Combo combo in combos)
            {
                ComboResult r = await RunComboAsync(endpoint!, credential, connectionString, authMode, combo, timeoutSeconds);
                results.Add(r);
                PrintResultLine(r);

                if (r.Outcome == Outcome.Hang)
                {
                    Console.WriteLine();
                    Console.WriteLine("================ HANG DETECTED ================");
                    Console.WriteLine($"Combo: {combo.Id}  ({combo.Describe()})");
                    Console.WriteLine($"ReadAccountAsync did not complete within {timeoutSeconds}s.");
                    Console.WriteLine();
                    Console.WriteLine($"CAPTURE A STACK NOW (process is still alive, PID {Environment.ProcessId}):");
                    Console.WriteLine($"    dotnet-stack report -p {Environment.ProcessId}");
                    Console.WriteLine($"    dotnet-dump collect -p {Environment.ProcessId}");
                    Console.WriteLine();
                    Console.WriteLine("---- last SDK trace/EventSource activity before the stall ----");
                    foreach (string line in EventCollector.Snapshot())
                    {
                        Console.WriteLine("  " + line);
                    }
                    Console.WriteLine("==============================================");
                    Console.WriteLine("Stopping the matrix (the hung call cannot be cancelled). " +
                                      "The winning bisection signal is which toggles were active above.");
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine("================ SUMMARY ================");
            foreach (ComboResult r in results)
            {
                Console.WriteLine($"  {r.Combo.Id,-26} {r.Outcome,-6} {r.Elapsed.TotalMilliseconds,8:F0} ms  {r.Detail}");
            }
            Console.WriteLine();
            Console.WriteLine("Interpretation:");
            Console.WriteLine("  * A 'baseline-*' combo hangs but its 'hedgeoff-*' counterpart is OK  => PR #5999 metadata hedging implicated.");
            Console.WriteLine("  * A 'baseline-*' combo hangs but its 'thinoff-*'  counterpart is OK  => PR #5970 thin client implicated (egress/port 10250).");
            Console.WriteLine("  * Hang persists with BOTH off                                        => not #5999/#5970 (AAD/Direct/connectivity, H4).");

            return results.Any(r => r.Outcome == Outcome.Hang) ? 1 : 0;
        }

        private static async Task<ComboResult> RunComboAsync(
            string endpoint, TokenCredential? credential, string? connectionString, string authMode, Combo combo, int timeoutSeconds)
        {
            // These env vars are read by ConfigurationManager at client construction.
            SetEnv("AZURE_COSMOS_METADATA_HEDGING_ENABLED", combo.MetadataHedging);
            SetEnv("AZURE_COSMOS_THIN_CLIENT_ENABLED", combo.ThinClient);

            Console.WriteLine($">>> {combo.Id}: {combo.Describe()}");

            CosmosClient client;
            try
            {
                client = BuildClient(endpoint, credential, connectionString, authMode, combo);
            }
            catch (Exception ex)
            {
                return new ComboResult(combo, Outcome.Error, TimeSpan.Zero, $"ctor: {ex.GetType().Name}: {ex.Message}");
            }

            var sw = Stopwatch.StartNew();
            // ReadAccountAsync() has no CancellationToken overload, so use a watchdog race.
            Task<AccountProperties> readTask = client.ReadAccountAsync();
            Task delayTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            Task finished = await Task.WhenAny(readTask, delayTask);
            sw.Stop();

            if (finished == delayTask)
            {
                // Do NOT dispose the client / return — leave the hung task so a dump can be taken.
                return new ComboResult(combo, Outcome.Hang, sw.Elapsed, "watchdog fired");
            }

            try
            {
                AccountProperties account = await readTask;
                string regions = string.Join(", ", account.WritableRegions.Select(r => r.Name)
                    .Concat(account.ReadableRegions.Select(r => r.Name)).Distinct());

                if (!DoContainerRead)
                {
                    client.Dispose();
                    return new ComboResult(combo, Outcome.Ok, sw.Elapsed, $"account='{account.Id}' regions=[{regions}]");
                }

                // Exercise the ACTUALLY-hedged path: a Collection Read (ReadContainerAsync). On a
                // multi-region account this is eligible for #5999 hedging; a hedge only *fires* if
                // the primary exceeds the ~1.5s threshold. Run it under a watchdog too.
                (Outcome cOutcome, TimeSpan cElapsed, string cDetail) = await ReadContainerWithProbeAsync(client, timeoutSeconds);
                client.Dispose();
                return new ComboResult(combo, cOutcome, cElapsed,
                    $"account regions=[{regions}]; containerRead: {cDetail}");
            }
            catch (Exception ex)
            {
                client.Dispose();
                return new ComboResult(combo, Outcome.Error, sw.Elapsed, $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads container metadata (Collection Read — the #5999-hedged path) under a watchdog, and
        /// reports timing plus whether a cross-region metadata hedge actually fired (from diagnostics).
        /// Best-effort provisions a throwaway db/container; falls back to an existing one if DDL is
        /// not permitted for the identity.
        /// </summary>
        private static async Task<(Outcome, TimeSpan, string)> ReadContainerWithProbeAsync(CosmosClient client, int timeoutSeconds)
        {
            Container? container = null;
            string provisionNote;
            try
            {
                Database db = await client.CreateDatabaseIfNotExistsAsync(DbName, throughput: 400);
                container = await db.CreateContainerIfNotExistsAsync(ContainerName, "/pk", throughput: 400);
                provisionNote = $"db='{DbName}' container='{ContainerName}'";
            }
            catch (Exception ex)
            {
                // No DDL RBAC (or similar): fall back to any existing container on the account.
                provisionNote = $"provision failed ({ex.GetType().Name}); enumerating existing";
                try
                {
                    (Database? db, Container? existing) = await FindAnyContainerAsync(client);
                    if (existing == null)
                    {
                        return (Outcome.Error, TimeSpan.Zero, $"{provisionNote}; no container available to read");
                    }
                    container = existing;
                    provisionNote = $"using existing db='{db!.Id}' container='{existing.Id}'";
                }
                catch (Exception ex2)
                {
                    return (Outcome.Error, TimeSpan.Zero, $"{provisionNote}; enumerate failed: {ex2.GetType().Name}: {ex2.Message}");
                }
            }

            // Read container metadata several times: the first populates the cache (cold), the rest
            // hit the steady-state refresh path — both are hedged by #5999.
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                bool anyHedgeFired = false;
                for (int i = 0; i < 5; i++)
                {
                    ContainerResponse resp = await container.ReadContainerAsync(requestOptions: null, cancellationToken: cts.Token);
                    string diag = resp.Diagnostics?.ToString() ?? string.Empty;
                    if (diag.Contains(MetadataHedgeMarker, StringComparison.OrdinalIgnoreCase))
                    {
                        anyHedgeFired = true;
                    }
                }
                sw.Stop();
                return (Outcome.Ok, sw.Elapsed, $"{provisionNote}; 5x ReadContainer ok; hedgeFired={anyHedgeFired}");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                return (Outcome.Hang, sw.Elapsed, $"{provisionNote}; ReadContainer watchdog fired at {timeoutSeconds}s");
            }
            catch (Exception ex)
            {
                sw.Stop();
                return (Outcome.Error, sw.Elapsed, $"{provisionNote}; {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static async Task<(Database?, Container?)> FindAnyContainerAsync(CosmosClient client)
        {
            using FeedIterator<DatabaseProperties> dbIter = client.GetDatabaseQueryIterator<DatabaseProperties>();
            while (dbIter.HasMoreResults)
            {
                foreach (DatabaseProperties dbp in await dbIter.ReadNextAsync())
                {
                    Database db = client.GetDatabase(dbp.Id);
                    using FeedIterator<ContainerProperties> cIter = db.GetContainerQueryIterator<ContainerProperties>();
                    while (cIter.HasMoreResults)
                    {
                        foreach (ContainerProperties cp in await cIter.ReadNextAsync())
                        {
                            return (db, db.GetContainer(cp.Id));
                        }
                    }
                }
            }

            return (null, null);
        }

        private const string MetadataHedgeMarker = "Metadata Hedge";

        /// <summary>
        /// Definitive test of #5999's hedge machinery: inject a 6s ResponseDelay on the PRIMARY
        /// region's Collection Read (well past the 1.5s hedge threshold) with metadata hedging
        /// force-enabled, then issue a cold metadata read and confirm the operation COMPLETES (does
        /// not hang), a hedge fired, and a SECONDARY region won. Mirrors the PR's own
        /// MetadataHedgingIntegrationTests hedge-forcing technique.
        /// </summary>
        private static async Task<int> RunFaultInjectionAsync(
            string endpoint, TokenCredential? credential, string? connectionString, string authMode, int timeoutSeconds)
        {
            string region1 = Environment.GetEnvironmentVariable("COSMOS_REGION1") ?? "West US 2";   // primary (delayed)
            string region2 = Environment.GetEnvironmentVariable("COSMOS_REGION2") ?? "West US 3";   // hedge target
            TimeSpan primaryDelay = TimeSpan.FromSeconds(6);

            Console.WriteLine("==== FAULT INJECTION: force a metadata hedge to fire ====");
            Console.WriteLine($"Primary region (delayed {primaryDelay.TotalSeconds}s) = {region1};  hedge target = {region2}");

            // 1) Discover a container to read (cold Collection Read) using a plain client.
            CosmosClient discoverClient = authMode == "key"
                ? new CosmosClient(connectionString, new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct })
                : new CosmosClient(endpoint, credential, new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct });
            (Database? probeDb, Container? probeContainer) = await FindAnyContainerAsync(discoverClient);
            if (probeContainer == null)
            {
                Console.Error.WriteLine("ERROR: no container found to exercise the hedged Collection Read.");
                discoverClient.Dispose();
                return 2;
            }
            string dbId = probeDb!.Id;
            string containerId = probeContainer.Id;
            discoverClient.Dispose();
            Console.WriteLine($"Target container: db='{dbId}' container='{containerId}'");

            // 2) Build the primary-region delay rule (starts disabled) for the Collection Read.
            string ruleId = $"metadata-hedge-delay-{Guid.NewGuid()}";
            FaultInjectionRule delayRule = new FaultInjectionRuleBuilder(
                id: ruleId,
                condition: new FaultInjectionConditionBuilder()
                    .WithOperationType(FaultInjectionOperationType.MetadataContainer)
                    .WithRegion(region1)
                    .Build(),
                result: FaultInjectionResultBuilder
                    .GetResultBuilder(FaultInjectionServerErrorType.ResponseDelay)
                    .WithDelay(primaryDelay)
                    .Build())
                .WithDuration(TimeSpan.FromMinutes(5))
                .Build();
            delayRule.Disable();

            // 3) Force metadata hedging ON regardless of PPAF, and build a fresh (cold) FI client.
            Environment.SetEnvironmentVariable("AZURE_COSMOS_METADATA_HEDGING_ENABLED", "true");
            FaultInjector faultInjector = new FaultInjector(new List<FaultInjectionRule> { delayRule });
            var options = new CosmosClientOptions
            {
                ConnectionMode = ConnectionMode.Direct,
                ApplicationPreferredRegions = new List<string> { region1, region2 },
                FaultInjector = faultInjector,
            };
            CosmosClient fiClient = authMode == "key"
                ? new CosmosClient(connectionString, options)
                : new CosmosClient(endpoint, credential, options);

            Container target = fiClient.GetDatabase(dbId).GetContainer(containerId);

            // 4) Enable the delay, then trigger the cold metadata read under a watchdog.
            delayRule.Enable();
            var sw = Stopwatch.StartNew();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var diag = new StringBuilder();
            try
            {
                using FeedIterator<dynamic> it = target.GetItemQueryIterator<dynamic>("SELECT * FROM c");
                while (it.HasMoreResults)
                {
                    FeedResponse<dynamic> page = await it.ReadNextAsync(cts.Token);
                    diag.AppendLine(page.Diagnostics.ToString());
                }
                sw.Stop();
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Console.WriteLine();
                Console.WriteLine("================ HANG DETECTED (fault injection) ================");
                Console.WriteLine($"The hedged Collection Read did NOT complete within {timeoutSeconds}s while the");
                Console.WriteLine($"primary region {region1} was delayed. PID {Environment.ProcessId} — capture now:");
                Console.WriteLine($"    dotnet-stack report -p {Environment.ProcessId}");
                delayRule.Disable();
                return 1;
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine($"Fault-injection read errored in {sw.ElapsedMilliseconds} ms: {ex.GetType().Name}: {ex.Message}");
                delayRule.Disable();
                fiClient.Dispose();
                Environment.SetEnvironmentVariable("AZURE_COSMOS_METADATA_HEDGING_ENABLED", null);
                return 1;
            }

            delayRule.Disable();
            string diagText = diag.ToString();
            bool hedgeFired = diagText.Contains(MetadataHedgeMarker) && diagText.Contains("HedgeFired=True");
            bool secondaryWon = diagText.Contains($"WinningRegion={region2}");
            long hitCount = delayRule.GetHitCount();

            Console.WriteLine();
            Console.WriteLine("================ FAULT INJECTION RESULT ================");
            Console.WriteLine($"Completed (NO hang) in {sw.ElapsedMilliseconds} ms.");
            Console.WriteLine($"Primary-delay rule hit count = {hitCount} (primary {region1} was slowed {primaryDelay.TotalSeconds}s).");
            Console.WriteLine($"Hedge fired  = {hedgeFired}");
            Console.WriteLine($"Secondary ({region2}) won = {secondaryWon}");
            Console.WriteLine();
            Console.WriteLine("Interpretation:");
            Console.WriteLine("  * Completed + hedgeFired=True + secondary won  => #5999 hedge machinery works and does NOT hang");
            Console.WriteLine("    even when the primary region is slow (the only condition under which a hedge fires).");
            Console.WriteLine("  * HANG DETECTED above                          => a real #5999 defect (capture the stack).");

            fiClient.Dispose();
            Environment.SetEnvironmentVariable("AZURE_COSMOS_METADATA_HEDGING_ENABLED", null);
            return 0;
        }

        private static CosmosClient BuildClient(string endpoint, TokenCredential? credential, string? connectionString, string authMode, Combo combo)
        {
            var clientOptions = new CosmosClientOptions
            {
                ApplicationName = ApplicationName,
                ConnectionMode = ConnectionMode.Direct,
                MaxRetryAttemptsOnRateLimitedRequests = MaxRetryAttemptsOnThrottledRequests,
                MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(MaxRetryWaitTimeInSeconds),
                OpenTcpConnectionTimeout = TimeSpan.FromSeconds(OpenTcpConnectionTimeoutInSec),
                RequestTimeout = TimeSpan.FromSeconds(RequestTimeoutInSeconds),
                LimitToEndpoint = false,
                EnableTcpConnectionEndpointRediscovery = true,
                PortReuseMode = PortReuseMode.PrivatePortPool,
            };

            // SDK hedging (data-plane availability strategy), as in the customer snippet.
            clientOptions.AvailabilityStrategy = AvailabilityStrategy.CrossRegionHedgingStrategy(
                threshold: HedgingThreshold,
                thresholdStep: HedgingThresholdStep,
                enableMultiWriteRegionHedge: false);

            // Internal props via reflection, matching the customer's DocumentDBClient behavior.
            SetInternalPropertyValueOnClientOptions(clientOptions, "EnableUpgradeConsistencyToLocalQuorum", true);
            SetInternalPropertyValueOnClientOptions(clientOptions, "EnableAdvancedReplicaSelectionForTcp", false);

            if (!string.IsNullOrEmpty(combo.ApplicationRegion))
            {
                clientOptions.ApplicationRegion = combo.ApplicationRegion;
            }

            if (combo.PreferredRegions != null)
            {
                clientOptions.ApplicationPreferredRegions = combo.PreferredRegions;
            }

            if (combo.UseLastCommittedSingleWriteRegion)
            {
                // ReadConsistencyStrategy is internal in GA (public in PREVIEW); set via reflection so the
                // harness compiles against either. LastCommittedSingleWriteRegion = 5.
                Type optionsType = clientOptions.GetType();
                PropertyInfo? rcsProp = optionsType.GetProperty("ReadConsistencyStrategy",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (rcsProp != null)
                {
                    Type enumType = Nullable.GetUnderlyingType(rcsProp.PropertyType) ?? rcsProp.PropertyType;
                    if (enumType.IsEnum)
                    {
                        object enumValue = Enum.ToObject(enumType, 5);
                        rcsProp.SetValue(clientOptions, enumValue, null);
                    }
                }
                else
                {
                    Console.WriteLine("    Warning: 'ReadConsistencyStrategy' not found on CosmosClientOptions.");
                }
            }

            return authMode == "key"
                ? new CosmosClient(connectionString, clientOptions)
                : new CosmosClient(endpoint, credential, clientOptions);
        }

        private static void SetInternalPropertyValueOnClientOptions(CosmosClientOptions options, string propertyName, object value)
        {
            PropertyInfo? prop = typeof(CosmosClientOptions).GetProperty(
                propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop == null)
            {
                Console.WriteLine($"    Warning: property '{propertyName}' not found on CosmosClientOptions.");
                return;
            }

            prop.SetValue(options, value, null);
        }

        private static TokenCredential BuildCredential()
        {
            string? tenantId = Environment.GetEnvironmentVariable("COSMOS_TENANT_ID");
            string? clientId = Environment.GetEnvironmentVariable("COSMOS_CLIENT_ID");
            string? certPath = Environment.GetEnvironmentVariable("COSMOS_CLIENT_CERT_PATH");

            if (!string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(certPath))
            {
                Console.WriteLine("Credential: ClientCertificateCredential (mirrors the customer's cert-based AAD).");
                return new ClientCertificateCredential(tenantId, clientId, certPath);
            }

            // The target account may trust a DIFFERENT AAD tenant than the CLI's default. When
            // COSMOS_AAD_TENANT_ID is set, mint the token from that tenant via the Azure CLI
            // (which is already signed in to it).
            string? aadTenant = Environment.GetEnvironmentVariable("COSMOS_AAD_TENANT_ID");
            if (!string.IsNullOrEmpty(aadTenant))
            {
                Console.WriteLine($"Credential: AzureCliCredential (tenant {aadTenant}).");
                return new AzureCliCredential(new AzureCliCredentialOptions { TenantId = aadTenant });
            }

            Console.WriteLine("Credential: DefaultAzureCredential (az login / managed identity / env).");
            return new DefaultAzureCredential();
        }

        private static List<Combo> BuildMatrix()
        {
            var combos = new List<Combo>();
            (string id, string? appRegion, IReadOnlyList<string>? preferred)[] shapes =
            {
                ("nonunified", "East US 2", null),
                ("unified", null, new[] { "East US 2", "Central US" }),
            };

            foreach ((string id, string? appRegion, IReadOnlyList<string>? preferred) shape in shapes)
            {
                // baseline = both preview features at their default (ON); reproduces the customer setup.
                combos.Add(new Combo($"baseline-{shape.id}", shape.appRegion, shape.preferred, MetadataHedging: null, ThinClient: null, false));
                // bisection legs.
                combos.Add(new Combo($"hedgeoff-{shape.id}", shape.appRegion, shape.preferred, MetadataHedging: false, ThinClient: null, false));
                combos.Add(new Combo($"thinoff-{shape.id}", shape.appRegion, shape.preferred, MetadataHedging: null, ThinClient: false, false));
            }

            // Unified read (ReadConsistencyStrategy=LastCommittedSingleWriteRegion) with everything default-on.
            combos.Add(new Combo("rcs-unified", null, new[] { "East US 2", "Central US" }, MetadataHedging: null, ThinClient: null, UseLastCommittedSingleWriteRegion: true));
            // Full kill-switch control: both preview features OFF, baseline shape.
            combos.Add(new Combo("alloff-unified", null, new[] { "East US 2", "Central US" }, MetadataHedging: false, ThinClient: false, false));

            return combos;
        }

        private static void PrintResultLine(ComboResult r)
        {
            Console.WriteLine($"    -> {r.Outcome} in {r.Elapsed.TotalMilliseconds:F0} ms  {r.Detail}");
            Console.WriteLine();
        }

        private static void SetEnv(string name, bool? value)
        {
            Environment.SetEnvironmentVariable(name, value.HasValue ? value.Value.ToString().ToLowerInvariant() : null);
        }

        private static int GetIntArg(string[] args, string name, int fallback)
        {
            int i = Array.IndexOf(args, name);
            return (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int v)) ? v : fallback;
        }

        private static string? GetStrArg(string[] args, string name)
        {
            int i = Array.IndexOf(args, name);
            return (i >= 0 && i + 1 < args.Length) ? args[i + 1] : null;
        }

        private enum Outcome { Ok, Hang, Error }

        private sealed record Combo(
            string Id,
            string? ApplicationRegion,
            IReadOnlyList<string>? PreferredRegions,
            bool? MetadataHedging,
            bool? ThinClient,
            bool UseLastCommittedSingleWriteRegion)
        {
            public string Describe()
            {
                string shape = ApplicationRegion != null
                    ? $"ApplicationRegion={ApplicationRegion}"
                    : $"ApplicationPreferredRegions=[{string.Join(", ", PreferredRegions ?? Array.Empty<string>())}]";
                string hedge = MetadataHedging switch { true => "on", false => "OFF", null => "default(on-preview)" };
                string thin = ThinClient switch { true => "on", false => "OFF", null => "default(on-preview)" };
                string rcs = UseLastCommittedSingleWriteRegion ? "; ReadConsistencyStrategy=LastCommittedSingleWriteRegion" : "";
                return $"{shape}; metadataHedging={hedge}; thinClient={thin}{rcs}";
            }
        }

        private sealed record ComboResult(Combo Combo, Outcome Outcome, TimeSpan Elapsed, string Detail);

        /// <summary>
        /// Demonstrates the PR #5549 cache-bypass fingerprint: wraps the real credential and records
        /// every TokenRequestContext the SDK passes to GetTokenAsync during a ReadAccountAsync + a
        /// container read. With #5549 present, Claims is NON-EMPTY (carries cp1) on every acquire,
        /// which forces Azure.Identity/MSAL to skip its token cache and call ESTS live each time.
        /// In 3.61.0 the SDK passed the scope context with NO claims, so MSAL served from cache.
        /// </summary>
        private static async Task<int> RunTokenProbeAsync(
            string endpoint, TokenCredential? credential, string? connectionString, string authMode, int timeoutSeconds)
        {
            if (authMode != "aad" || credential == null)
            {
                Console.Error.WriteLine("ERROR: --tokenprobe requires --auth aad.");
                return 2;
            }

            var probe = new ProbeCredential(credential);
            CosmosClient client = new CosmosClient(endpoint, probe, new CosmosClientOptions { ConnectionMode = ConnectionMode.Direct });

            Console.WriteLine("==== TOKEN PROBE: recording every TokenRequestContext the SDK passes ====");
            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                await client.ReadAccountAsync().WaitAsync(cts.Token);
                Console.WriteLine($"ReadAccountAsync ok in {sw.ElapsedMilliseconds} ms.");

                (Database? db, Container? c) = await FindAnyContainerAsync(client);
                if (c != null)
                {
                    await c.ReadContainerAsync(requestOptions: null, cancellationToken: cts.Token);
                    Console.WriteLine($"ReadContainerAsync ok (db='{db!.Id}', container='{c.Id}').");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Probe operation ended: {ex.GetType().Name}: {ex.Message}");
            }
            sw.Stop();

            Console.WriteLine();
            Console.WriteLine("================ TOKEN PROBE RESULT ================");
            Console.WriteLine($"Total GetTokenAsync calls observed = {probe.Calls.Count}");
            int withClaims = probe.Calls.Count(c => c.HasClaims);
            Console.WriteLine($"Calls carrying a NON-EMPTY Claims (cp1 cache-bypass fingerprint) = {withClaims}/{probe.Calls.Count}");
            foreach (ProbeCredential.CallInfo ci in probe.Calls)
            {
                Console.WriteLine($"  #{ci.Index}: IsCaeEnabled={ci.IsCaeEnabled}; HasClaims={ci.HasClaims}; claims='{ci.ClaimsPreview}'");
            }
            Console.WriteLine();
            Console.WriteLine("Interpretation:");
            Console.WriteLine("  * HasClaims=True on acquisitions => PR #5549 cache-bypass fingerprint present.");
            Console.WriteLine("    With an MSAL-backed credential (e.g. ClientCertificateCredential, ManagedIdentity),");
            Console.WriteLine("    a non-empty Claims makes AcquireTokenSilent SKIP the cache and call ESTS live on");
            Console.WriteLine("    EVERY acquire. cp1/CAE is ALREADY advertised via isCaeEnabled=true, so the Claims");
            Console.WriteLine("    injection is redundant AND the cause of the regression.");
            Console.WriteLine("  * 3.61.0 passed NO claims here, so MSAL served the token from cache (fast, no hang).");

            client.Dispose();
            return 0;
        }

        /// <summary>Credential wrapper that records the TokenRequestContext of every acquisition, then delegates.</summary>
        private sealed class ProbeCredential : TokenCredential
        {
            private readonly TokenCredential inner;
            private int counter;
            public System.Collections.Generic.List<CallInfo> Calls { get; } = new System.Collections.Generic.List<CallInfo>();

            public ProbeCredential(TokenCredential inner) => this.inner = inner;

            public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.Record(requestContext);
                return this.inner.GetToken(requestContext, cancellationToken);
            }

            public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            {
                this.Record(requestContext);
                return await this.inner.GetTokenAsync(requestContext, cancellationToken);
            }

            private void Record(TokenRequestContext ctx)
            {
                string? claims = ctx.Claims;
                bool hasClaims = !string.IsNullOrEmpty(claims);
                lock (this.Calls)
                {
                    this.Calls.Add(new CallInfo(
                        ++this.counter,
                        ctx.IsCaeEnabled,
                        hasClaims,
                        hasClaims ? (claims!.Length > 60 ? claims.Substring(0, 60) + "..." : claims) : ""));
                }
            }

            public readonly record struct CallInfo(int Index, bool IsCaeEnabled, bool HasClaims, string ClaimsPreview);
        }

        /// <summary>
        /// Subscribes to every EventSource in the process and keeps a bounded ring buffer of events
        /// coming from Cosmos / Azure sources, so a hang report shows the last SDK activity before the stall.
        /// </summary>
        private sealed class SdkEventCollector : EventListener
        {
            private const int Capacity = 200;
            private readonly ConcurrentQueue<string> buffer = new ConcurrentQueue<string>();

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                string name = eventSource.Name ?? string.Empty;
                if (name.Contains("Cosmos", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Azure", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("DocDB", StringComparison.OrdinalIgnoreCase)
                    || name.Contains("Http", StringComparison.OrdinalIgnoreCase))
                {
                    this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs e)
            {
                string payload = e.Payload != null ? string.Join(" | ", e.Payload) : string.Empty;
                string line = $"{DateTime.UtcNow:HH:mm:ss.fff} [{e.EventSource.Name}/{e.EventName}] {payload}";
                this.buffer.Enqueue(line);
                while (this.buffer.Count > Capacity && this.buffer.TryDequeue(out _)) { }
            }

            public IReadOnlyList<string> Snapshot() => this.buffer.ToArray();
        }
    }
}
