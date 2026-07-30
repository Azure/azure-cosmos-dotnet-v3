//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Shared plumbing for the live-account AAD (Microsoft Entra ID) test classes
    /// (<see cref="CosmosAadLiveTests"/>, <see cref="CosmosAadLiveDataPlaneTests"/>).
    ///
    /// All of those tests authenticate to a real, AAD-only Cosmos DB account with a real
    /// <see cref="global::Azure.Core.TokenCredential"/> holding only the data-plane role
    /// (Cosmos DB Built-in Data Contributor).
    ///
    /// Fixture contract
    /// ----------------
    /// The Cosmos DB data-plane permission model grants <c>readMetadata</c>, container-level actions
    /// (query / change feed / stored procedure / conflicts) and item actions -- it does NOT grant
    /// database or container creation, which are control-plane operations. A data-plane-only token
    /// therefore cannot provision anything at run time (see
    /// <see cref="CosmosAadLiveTests.AadControlPlaneIsForbiddenAsync"/>, which asserts the 403).
    ///
    /// That is the one structural difference from the Python SDK's AAD lane, which keeps a
    /// master-key client around for control-plane setup. This account has key auth disabled, so
    /// <see cref="DatabaseId"/> / <see cref="ContainerId"/> (partition key <c>/pk</c>) must be
    /// pre-created out of band and every test has to fit inside that single container.
    /// </summary>
    internal static class AadLiveTestSupport
    {
        /// <summary>
        /// Pre-created database on the live AAD account. Cannot be created by the tests: database
        /// creation is a control-plane operation that a data-plane-only token is denied.
        /// </summary>
        internal const string DatabaseId = "AadLiveTestDb";

        /// <summary>
        /// Pre-created container (partition key path <c>/pk</c>) on the live AAD account. Every live
        /// AAD test shares it, so tests must scope their data with a unique partition key
        /// (<see cref="NewPartitionKeyValue(string)"/>) and clean up after themselves.
        /// </summary>
        internal const string ContainerId = "AadLiveTestContainer";

        /// <summary>
        /// Partition key path of <see cref="ContainerId"/>.
        /// </summary>
        internal const string PartitionKeyPath = "/pk";

        /// <summary>
        /// MSTest category that selects the live AAD lane (<c>templates/build-test-aad.yml</c>).
        /// </summary>
        internal const string TestCategory = "MultiRegionAad";

        /// <summary>
        /// The total number of <c>MultiRegionAad</c> test cases across every live AAD test class, counting
        /// each <see cref="DataRowAttribute"/> separately. The CI lane gates on exactly this many passing
        /// with zero skipped/inconclusive results, so keep the <c>ExpectedTestCount</c> parameter in
        /// <c>templates/build-test-aad.yml</c> in sync when adding or removing cases.
        ///
        /// <see cref="CosmosAadLiveTestsGateTests"/> runs in the ordinary emulator lane and fails when this
        /// drifts from what is actually declared, so the mismatch is caught before the live lane runs.
        /// </summary>
        internal const int ExpectedTestCaseCount = 36;

        /// <summary>
        /// Reports an unsatisfied prerequisite for the live AAD tests.
        ///
        /// By default the case is skipped via <see cref="Assert.Inconclusive(string)"/>, which keeps local,
        /// opt-in runs green for developers who have not provisioned the AAD account. When
        /// <c>COSMOSDB_AAD_STRICT</c> is set (the dedicated CI lane does so), the same condition becomes a
        /// hard failure instead: an inconclusive run still lets MSTest exit successfully, so without this a
        /// missing role assignment, a stale endpoint, or a missing fixture could leave the lane green
        /// without validating a single Entra-authenticated operation.
        /// </summary>
        internal static void SkipOrFail(string message)
        {
            if (TestCommon.IsAadStrictMode())
            {
                Assert.Fail($"COSMOSDB_AAD_STRICT is enabled, so an unsatisfied live AAD prerequisite is a failure rather than a skip: {message}");
            }
            else
            {
                Assert.Inconclusive(message);
            }
        }

        /// <summary>
        /// Verifies that the endpoint and an Entra credential are configured, skipping (locally) or failing
        /// (in strict/CI mode) when they are not.
        /// </summary>
        internal static void ValidateConfiguration()
        {
            if (string.IsNullOrEmpty(TestCommon.GetAadAccountEndpoint()))
            {
                AadLiveTestSupport.SkipOrFail("Set COSMOSDB_MULTI_REGION_AAD (or COSMOSDB_MULTI_REGION) to the AAD account endpoint to run the live AAD tests.");
            }

            if (TestCommon.GetAadTokenCredential() == null)
            {
                AadLiveTestSupport.SkipOrFail("Set AZURE_TENANT_ID / AZURE_CLIENT_ID / AZURE_CLIENT_SECRET (or COSMOSDB_AAD_USE_DEFAULT_CREDENTIAL=true) to run the live AAD tests.");
            }
        }

        /// <summary>
        /// Creates an Entra-authenticated <see cref="CosmosClient"/> for the live AAD account, asserting
        /// rather than returning null when the account/credentials are not configured (callers are expected
        /// to have run <see cref="ValidateConfiguration"/> first).
        /// </summary>
        internal static CosmosClient CreateClient(CosmosClientOptions clientOptions = null)
        {
            CosmosClient client = TestCommon.CreateAadCosmosClient(clientOptions);
            Assert.IsNotNull(client, "Live AAD account/credentials are not configured.");
            return client;
        }

        /// <summary>
        /// Confirms the pre-created fixture is reachable with the current Entra token by issuing a
        /// data-plane metadata read, translating the two expected setup failures (missing resources,
        /// missing role assignment) into an actionable skip/failure.
        /// </summary>
        internal static async Task ValidateFixtureAsync(Container container)
        {
            try
            {
                await container.ReadContainerAsync();
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                AadLiveTestSupport.SkipOrFail($"Pre-create database '{AadLiveTestSupport.DatabaseId}' and container '{AadLiveTestSupport.ContainerId}' ({AadLiveTestSupport.PartitionKeyPath}) on the AAD account before running these tests. Response: {ex.Message}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden || ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                AadLiveTestSupport.SkipOrFail($"The AAD principal is missing the Cosmos DB data-plane role assignment (Cosmos DB Built-in Data Contributor). Response: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a partition key value that is unique to a single test run. The live AAD tests all share
        /// one long-lived container, so isolating each test behind its own partition key is what keeps a
        /// concurrent or previously-failed run from changing another test's result.
        /// </summary>
        internal static string NewPartitionKeyValue(string prefix)
        {
            return $"{prefix}-{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Reads an item, tolerating a bounded window of replication lag.
        ///
        /// A read that is deliberately routed away from the region a write landed in -- via
        /// <see cref="RequestOptions.ExcludeRegions"/>, <see cref="CosmosClientOptions.ApplicationRegion"/>,
        /// <see cref="CosmosClientOptions.ApplicationPreferredRegions"/> or cross-region hedging -- can reach
        /// a replica before the write has replicated. Under anything weaker than strong consistency that is
        /// correct service behaviour, and it surfaces as a 404 (or, under session consistency, as a 404 after
        /// the SDK exhausts its <c>ReadSessionNotAvailable</c> retries).
        ///
        /// The live AAD lane's result gate requires every case to pass with zero skips, so region-routing
        /// tests must converge on replication instead of asserting that it has already happened. The retry
        /// budget is deliberately short: it absorbs normal replication lag without hiding a genuinely missing
        /// item, which still fails once the budget is spent.
        /// </summary>
        internal static async Task<ItemResponse<T>> ReadItemToleratingReplicationLagAsync<T>(
            Container container,
            string id,
            PartitionKey partitionKey,
            ItemRequestOptions requestOptions = null)
        {
            const int maxAttempts = 20;
            TimeSpan delayBetweenAttempts = TimeSpan.FromMilliseconds(500);

            CosmosException lastNotFound = null;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    return await container.ReadItemAsync<T>(id, partitionKey, requestOptions);
                }
                catch (CosmosException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
                {
                    lastNotFound = exception;
                    await Task.Delay(delayBetweenAttempts);
                }
            }

            throw new AssertFailedException(
                $"Item '{id}' was not readable within {maxAttempts * delayBetweenAttempts.TotalSeconds:0} seconds of being written. "
                + $"That is longer than cross-region replication should take, so this is a real failure rather than lag. Last response: {lastNotFound?.Message}",
                lastNotFound);
        }

        /// <summary>
        /// Best-effort deletion of items created by a test. Failures are ignored: the container is shared
        /// and long-lived, so leftover documents are undesirable but must never turn a passing assertion
        /// into a failing test (or mask the real failure when cleanup runs from a <c>finally</c> block).
        /// </summary>
        internal static Task CleanupItemsAsync(Container container, string partitionKeyValue, IEnumerable<string> itemIds)
        {
            List<(string Id, string PartitionKeyValue)> items = new List<(string, string)>();
            foreach (string id in itemIds)
            {
                items.Add((id, partitionKeyValue));
            }

            return AadLiveTestSupport.CleanupItemsAsync(container, items);
        }

        /// <summary>
        /// Best-effort deletion of items that span more than one partition key (cross-partition query and
        /// change feed tests). Failures are ignored for the reasons described on the other overload.
        /// </summary>
        internal static async Task CleanupItemsAsync(Container container, IEnumerable<(string Id, string PartitionKeyValue)> items)
        {
            foreach ((string id, string partitionKeyValue) in items)
            {
                try
                {
                    await container.DeleteItemAsync<ToDoActivity>(id, new PartitionKey(partitionKeyValue));
                }
                catch (CosmosException)
                {
                    // Ignored -- see summary.
                }
            }
        }

        /// <summary>
        /// Best-effort deletion of the items produced by <see cref="ToDoActivity"/> factories, keyed off each
        /// item's own <see cref="ToDoActivity.pk"/>.
        /// </summary>
        internal static Task CleanupItemsAsync(Container container, IEnumerable<ToDoActivity> items)
        {
            List<(string Id, string PartitionKeyValue)> ids = new List<(string, string)>();
            foreach (ToDoActivity item in items)
            {
                ids.Add((item.id, item.pk));
            }

            return AadLiveTestSupport.CleanupItemsAsync(container, ids);
        }
    }
}
