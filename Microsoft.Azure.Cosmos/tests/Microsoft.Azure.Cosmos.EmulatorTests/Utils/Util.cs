//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using OpenTelemetry;
    using OpenTelemetry.Trace;
    using AzureCore = global::Azure.Core;
    
    internal enum DocumentClientType
    {
        Gateway,
        DirectTcp,
        DirectHttps
    }

    internal sealed class Util
    {
        internal static readonly string sampleETag = "FooBar";

        internal static readonly List<string> validSessionTokens = new List<string>
        {
           null,
           string.Empty,
           // ReplicaProgressManager::StartTransaction returns E_RESOURCE_NOTFOUND when nCurrentLSN < nRequestLSN
           VersionUtility.IsLaterThan(HttpConstants.Versions.CurrentVersion, HttpConstants.Versions.v2018_06_18) ? "0:-1#1" : "0:1"
        };

        internal static readonly List<string> invalidSessionTokens = new List<string>
        {
           "1:rser",
           "FOOBAR",
           "123foobar",
           "123#%@#$%",
           "23323333333111111233233333331111112332333333311111123323333333111111233233333331111112332333333311111123323333333111111233233333331111112332333333311111123323333333111111233233333331111112332333333311111123323333333111111"
        };

        internal static readonly string invalidOfferType = "InvalidOfferType";

        internal static readonly List<int?> validOfferThroughputsForDatabase = new List<int?>
        {
                null,
                50000,
        };

        internal static readonly List<int?> invalidOfferThroughputsForDatabase = new List<int?>
        {
                0,
                399,
                450,
                999,
                1001,
                16000,
                55000,
        };


        internal static readonly List<int?> validOfferThroughputs = new List<int?>
        {
                null,
                400,
                500,
                900,
                1000,
                2500,
                5500,
                10000,
        };

        internal static readonly List<int?> invalidOfferThroughputs = new List<int?>
        {
                0,
                399,
                450,
                999,
                1001,
                16000,
                50000,
        };

        /// <summary>
        /// Helper function to run a test scenario for client of type DocumentClientType.
        /// </summary>
        /// <param name="testFunc"></param>
        /// <param name="testName"></param>
        /// <param name="requestChargeHelper"></param>
        /// <param name="authTokenType">authTokenType for the client created for running the test</param>
        internal static void TestForEachClient(
            Func<DocumentClient, DocumentClientType, Task> testFunc,
            string testName,
            RequestChargeHelper requestChargeHelper = null,
            AuthorizationTokenType authTokenType = AuthorizationTokenType.PrimaryMasterKey,
            ConsistencyLevel? consistencyLevel = null)
        {
            IDictionary<DocumentClientType, DocumentClient> clients =
                new Dictionary<DocumentClientType, DocumentClient>
            {
                {DocumentClientType.Gateway, TestCommon.CreateClient(true, tokenType: authTokenType, defaultConsistencyLevel: consistencyLevel)},
                {DocumentClientType.DirectTcp, TestCommon.CreateClient(false, Protocol.Tcp, tokenType: authTokenType, defaultConsistencyLevel: consistencyLevel)}
            };

            foreach (KeyValuePair<DocumentClientType, DocumentClient> clientEntry in clients)
            {
                try
                {
                    RunTestForClient(testFunc, testName, clientEntry.Key, clientEntry.Value);
                }
                finally
                {
                    clientEntry.Value.Dispose();
                }
            }

            requestChargeHelper?.CompareRequestCharge(testName);
        }

        internal static async Task DeleteAllDatabasesAsync(CosmosClient client,
            IEnumerable<string> excludeDbIds = null,
            bool deleteContainersOnExcludedDbs = true,
            ItemRequestOptions requestOptions = null)
        {
            if (client == null)
            {
                return;
            }

            QueryRequestOptions queryRequestIptions = new QueryRequestOptions()
            {
                OperationMetricsOptions = requestOptions?.OperationMetricsOptions,
                NetworkMetricsOptions = requestOptions?.NetworkMetricsOptions
            };

            using (FeedIterator<DatabaseProperties> feedIterator = client.GetDatabaseQueryIterator<DatabaseProperties>(requestOptions: queryRequestIptions))
            {
                while (feedIterator.HasMoreResults)
                {
                    FeedResponse<DatabaseProperties> response = await feedIterator.ReadNextAsync();
                    foreach (DatabaseProperties database in response)
                    {
                        Cosmos.Database db = client.GetDatabase(database.Id);
                        if (excludeDbIds?.Contains(database.Id) != true)
                        {
                            await db.DeleteAsync(requestOptions: requestOptions);
                        }
                        else if(deleteContainersOnExcludedDbs)
                        {
                            await DeleteAllContainersAsync(db, queryRequestIptions);
                        }
                    }
                }
            }
        }

        private static async Task DeleteAllContainersAsync(Cosmos.Database db, QueryRequestOptions queryRequestIptions = null)
        {
            using (FeedIterator<ContainerProperties> containerfeedIterator = db.GetContainerQueryIterator<ContainerProperties>(requestOptions: queryRequestIptions))
            {
                while (containerfeedIterator.HasMoreResults)
                {
                    FeedResponse<ContainerProperties> containerResponse = await containerfeedIterator.ReadNextAsync();
                    foreach (ContainerProperties container in containerResponse)
                    {
                        System.Diagnostics.Trace.TraceInformation($"Deleting container {container.Id}");
                        await db.GetContainer(container.Id).DeleteContainerAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to run a test scenario for a random client of type DocumentClientType.
        /// </summary>
        /// <param name="testFunc"></param>
        /// <param name="testName"></param>
        /// <param name="requestChargeHelper"></param>
        /// <param name="authTokenType">authTokenType for the client created for running the test</param>
        internal static void TestForAnyClient(
            Func<DocumentClient, DocumentClientType, Task> testFunc,
            string testName,
            RequestChargeHelper requestChargeHelper = null,
            AuthorizationTokenType authTokenType = AuthorizationTokenType.PrimaryMasterKey,
            ConsistencyLevel? consistencyLevel = null)
        {
            IDictionary<DocumentClientType, DocumentClient> clients =
                new Dictionary<DocumentClientType, DocumentClient>
            {
                {DocumentClientType.Gateway, TestCommon.CreateClient(true, tokenType: authTokenType, defaultConsistencyLevel: consistencyLevel)},
                {DocumentClientType.DirectTcp, TestCommon.CreateClient(false, Protocol.Tcp, tokenType: authTokenType, defaultConsistencyLevel: consistencyLevel)}
            };

            int seed = (int)DateTime.Now.Ticks;
            Random rand = new Random(seed);
            DocumentClientType selectedType = clients.Keys.ElementAt(rand.Next(clients.Count));
            DocumentClient client = clients[selectedType];

            try
            {
                RunTestForClient(testFunc, testName, selectedType, client);
            }
            finally
            {
                client.Dispose();
            }

            requestChargeHelper?.CompareRequestCharge(testName);
        }

        private static void RunTestForClient(
            Func<DocumentClient, DocumentClientType, Task> testFunc,
            string testName,
            DocumentClientType clientType,
            DocumentClient client)
        {
            try
            {
                Logger.LogLine("Run test - {0} for clientType {1}", testName, clientType);
                testFunc(client, clientType).Wait();
            }
            catch (Exception e)
            {
                Logger.LogLine("Run test {0} for clientType {1} failed with exception - {2}", testName, clientType, e.ToString());
                Assert.Fail(
                    string.Format(
                    CultureInfo.InvariantCulture,
                    "Test {0} failed for clientType - {1} with unexpected exception : {2}",
                    testFunc,
                    clientType,
                    e.ToString()));
            }
        }

        internal static IEnumerable<RequestOptions> GenerateAllPossibleRequestOptions(bool isDatabase = false)
        {
            List<AccessCondition> accessConditions = new List<AccessCondition>
            {
                new AccessCondition
                {
                    Type = AccessConditionType.IfNoneMatch,
                    Condition = null
                },
                new AccessCondition
                {
                    Type = AccessConditionType.IfNoneMatch,
                    Condition = string.Empty
                },
                new AccessCondition
                {
                    Type = AccessConditionType.IfNoneMatch,
                    Condition = " "
                },
                new AccessCondition
                {
                    Type = AccessConditionType.IfNoneMatch,
                    Condition = sampleETag
                },
                new AccessCondition
                {
                    Type = AccessConditionType.IfMatch,
                    Condition = null
                },
                 new AccessCondition
                {
                    Type = AccessConditionType.IfMatch,
                    Condition = sampleETag
                },
            };

            List<string> sessionTokens = new List<string>(validSessionTokens);
            sessionTokens.AddRange(invalidSessionTokens);

            List<string> offerTypes = new List<string>
            {
                null,
                string.Empty,
                "S1",
                "S2",
                "S3",
                invalidOfferType
            };

            List<int?> OfferThroughputs = new List<int?>(isDatabase ? validOfferThroughputsForDatabase : validOfferThroughputs);
            OfferThroughputs.AddRange(isDatabase ? invalidOfferThroughputsForDatabase : invalidOfferThroughputs);

            foreach (AccessCondition accessCondition in accessConditions)
            {
                foreach (ConsistencyLevel level in (ConsistencyLevel[])Enum.GetValues(typeof(ConsistencyLevel)))
                {
                    foreach (IndexingDirective policy in (IndexingDirective[])Enum.GetValues(typeof(IndexingDirective)))
                    {
                        foreach (string sessionToken in sessionTokens)
                        {
                            foreach (string offerType in offerTypes)
                            {
                                // The test is long running already(60mins+), hence for OfferThroughput we chose a lesser set of combinations that are interesting
                                //
                                if ((offerType == null)
                                    && sessionToken == null
                                    && policy == IndexingDirective.Default
                                    && level == ConsistencyLevel.Session
                                    && accessCondition.Equals(accessConditions[0]))
                                {
                                    foreach (int? offerThroughput in OfferThroughputs)
                                    {
                                        RequestOptions options = new RequestOptions
                                        {
                                            AccessCondition = accessCondition,
                                            ConsistencyLevel = level,
                                            IndexingDirective = policy,
                                            SessionToken = sessionToken,
                                            OfferType = offerType,
                                            OfferThroughput = offerThroughput
                                        };

                                        Util.LogRequestOptions(options);
                                        yield return options;
                                    }
                                }
                                else
                                {
                                    RequestOptions options = new RequestOptions
                                    {
                                        AccessCondition = accessCondition,
                                        ConsistencyLevel = level,
                                        IndexingDirective = policy,
                                        SessionToken = sessionToken,
                                        OfferType = offerType
                                    };

                                    Util.LogRequestOptions(options, false);
                                    yield return options;
                                }
                            }
                        }
                    }
                }
            }
        }

        internal static void ValidateCommonCustomHeaders(INameValueCollection headers)
        {
            Assert.IsNotNull(headers[HttpConstants.HttpHeaders.RequestCharge], "RequestCharge cannot be null");
            Assert.IsNotNull(headers[HttpConstants.HttpHeaders.MaxResourceQuota], "MaxResourceQuota cannot be null");
            Assert.IsNotNull(headers[HttpConstants.HttpHeaders.CurrentResourceQuotaUsage], "CurrentResourceQuotaUsage cannot be null");
            Assert.IsTrue(headers[HttpConstants.HttpHeaders.ServerVersion].Contains("version="), "ServerVersion must contain some version");
            Assert.AreNotEqual(Guid.Parse(headers[HttpConstants.HttpHeaders.ActivityId]), Guid.Empty, "ActivityId cannot be an empty GUID");
            Assert.IsNotNull(headers[HttpConstants.HttpHeaders.SchemaVersion], "SchemaVersion cannot be null");
            Assert.IsNotNull(headers[HttpConstants.HttpHeaders.LastStateChangeUtc], "LastStateChangeUtc cannot be null");
        }

        internal static void ValidateClientException(DocumentClientException ex, HttpStatusCode expectedStatusCode)
        {
            Assert.AreEqual(expectedStatusCode, ex.StatusCode, "Status code not as expected (ActivityId: {0})", ex.ActivityId);
            Assert.IsNotNull(ex.RequestCharge, "RequestCharge cannot be null (ActivityId: {0})", ex.ActivityId);
            Assert.AreEqual(TimeSpan.FromMilliseconds(0), ex.RetryAfter, "RetryAfter should be 0 (ActivityId: {0})", ex.ActivityId);
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(ex.ActivityId), "Activity ID should be an empty GUID (ActivityId: {0})", ex.ActivityId);
            Assert.AreEqual(expectedStatusCode.ToString(), ex.Error.Code, "Error code mismatch (ActivityId: {0})", ex.ActivityId);
            Assert.IsNotNull(ex.Error.Message, "Error message should not be null (ActivityId: {0})", ex.ActivityId);
        }

        internal static void ValidateResource(Resource resource)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.AltLink), "AltLink for a resource cannot be null or whitespace");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.ETag), "Etag for a resource cannot be null or whitespace");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.Id), "Id for a resource cannot be null or whitespace");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.ResourceId), "ResourceId for a resource cannot be null or whitespace");
            Assert.AreNotSame(resource.Id, resource.ResourceId, "ResourceId and Id for a resource cannot be same");
            Assert.IsFalse(string.IsNullOrWhiteSpace(resource.SelfLink), "SelfLink for a resource cannot be null or whitespace");
            Assert.AreNotSame(resource.SelfLink, resource.AltLink, "SelfLink and altLink for a resource cannot be same");
            Assert.IsTrue(resource.Timestamp > DateTime.MinValue, "Timestamp set for resource is not correct");
        }

        internal static void ValidateRestHeader(INameValueCollection responseHeader)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                HttpConstants.HttpHeaders.TransferEncoding),
                string.Format(CultureInfo.InvariantCulture, "{0} not set in response header", HttpConstants.HttpHeaders.TransferEncoding));
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                HttpConstants.HttpHeaders.StrictTransportSecurity),
                string.Format(CultureInfo.InvariantCulture, "{0} not set in response header", HttpConstants.HttpHeaders.StrictTransportSecurity));
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                HttpConstants.HttpHeaders.HttpDate),
                string.Format(CultureInfo.InvariantCulture, "{0} not set in response header", HttpConstants.HttpHeaders.HttpDate));
        }

        internal static async Task WaitForReIndexingToFinish(
            int maxWaitDurationInSeconds,
            DocumentCollection collection)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));

            int currentWaitSeconds = 0;
            DocumentClient[] lockedClients = ReplicationTests.GetClientsLocked();
            for (int index = 0; index < lockedClients.Length; ++index)
            {
                Logger.LogLine("Client: " + index);

                while (true)
                {
                    long reindexerProgress = (await TestCommon.AsyncRetryRateLimiting(
                        () => lockedClients[index].ReadDocumentCollectionAsync(collection.SelfLink, new RequestOptions { PopulateQuotaInfo = true }))).IndexTransformationProgress;
                    Logger.LogLine("Progress: " + reindexerProgress);
                    if (reindexerProgress == -1)
                    {
                        throw new Exception("Failed to obtain the reindexer progress.");
                    }
                    else if (reindexerProgress == 100)
                    {
                        Logger.LogLine("ReIndexing finished after: " + currentWaitSeconds + " seconds");
                        break;
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        currentWaitSeconds++;
                        Logger.LogLine("ReIndexing still running after: " + currentWaitSeconds + " seconds");
                        if (currentWaitSeconds > maxWaitDurationInSeconds)
                        {
                            throw new Exception("ReIndexing did not complete after: " + maxWaitDurationInSeconds + "  seconds");
                        }
                    }
                }
            }
        }

        internal static async Task WaitForLazyIndexingToCompleteAsync(DocumentCollection collection)
        {
            TimeSpan maxWaitTime = TimeSpan.FromMinutes(10);
            TimeSpan sleepTimeBetweenReads = TimeSpan.FromSeconds(1);

            // First wait for replication to complete
            TestCommon.WaitForServerReplication();

            DocumentClient[] lockedClients = ReplicationTests.GetClientsLocked();
            for (int index = 0; index < lockedClients.Length; ++index)
            {
                Logger.LogLine("Client: " + index);

                while (true)
                {
                    long lazyIndexingProgress = (await lockedClients[index].ReadDocumentCollectionAsync(collection.SelfLink, new RequestOptions { PopulateQuotaInfo = true })).LazyIndexingProgress;
                    if (lazyIndexingProgress == -1)
                    {
                        throw new Exception("Failed to obtain the lazy indexing progress.");
                    }
                    else if (lazyIndexingProgress == 100)
                    {
                        Logger.LogLine("Indexing completed at {0}", DateTime.Now.ToString("HH:mm:ss.f", CultureInfo.InvariantCulture));
                        break;
                    }
                    else
                    {
                        Logger.LogLine("Obtained the lazy indexing progress: {0}. Sleep for {1} seconds", lazyIndexingProgress, sleepTimeBetweenReads.TotalSeconds);
                        await Task.Delay(sleepTimeBetweenReads);
                        maxWaitTime -= sleepTimeBetweenReads;
                        if (maxWaitTime.TotalMilliseconds <= 0)
                        {
                            throw new Exception("Indexing didn't complete within the allocated time");
                        }
                    }
                }
            }
        }

        internal static async Task<StoredProcedure> GetOrCreateStoredProcedureAsync(
            DocumentClient client,
            DocumentCollection collection,
            StoredProcedure newStoredProcedure)
        {
            StoredProcedure result = (from proc in client.CreateStoredProcedureQuery(collection.SelfLink)
                          where proc.Id == newStoredProcedure.Id
                          select proc).AsEnumerable().FirstOrDefault();
            if (result != null)
            {
                return result;
            }

            return await client.CreateStoredProcedureAsync(collection.SelfLink, newStoredProcedure);
        }

        internal static void ValidateInvalidOfferThroughputException(DocumentClientException ex, HttpStatusCode expectedStatusCode)
        {
            Assert.AreEqual(expectedStatusCode, ex.StatusCode, "Status code not as expected (ActivityId: {0})", ex.ActivityId);
            Assert.IsNotNull(ex.RequestCharge, "RequestCharge cannot be null (ActivityId: {0})", ex.ActivityId);
            Assert.AreEqual(TimeSpan.FromMilliseconds(0), ex.RetryAfter, "RetryAfter should be 0 (ActivityId: {0})", ex.ActivityId);
            Assert.AreNotEqual(Guid.Empty, Guid.Parse(ex.ActivityId), "Activity ID should be an empty GUID (ActivityId: {0})", ex.ActivityId);
            Assert.AreEqual(expectedStatusCode.ToString(), ex.Error.Code, "Error code mismatch (ActivityId: {0})", ex.ActivityId);
            Assert.IsNotNull(ex.Error.Message, "Error message should not be null (ActivityId: {0})", ex.ActivityId);
        }

        internal static void ThrowsForbiddenExceptionWithMessage(Action action, string expecetedMessage)
        {
            string actualMessage = "";
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DocumentClientException dbcDbExp = ex as DocumentClientException;

                if (dbcDbExp == null)
                {
                    Assert.Fail($"Action did not throw DocumentClientException. Actual exception {ex.GetType()}");
                }

                if (dbcDbExp.Error.Code.Equals("Forbidden") && ex.Message.Contains(expecetedMessage))
                {
                    return;
                }

                actualMessage = ex.Message;
            }

            Assert.Fail($"Action did not throw DocumentClientException expected where the message contains {expecetedMessage}, Actual exception message: {actualMessage}");
        }

        internal static void ThrowsDocumentClientExceptionWithMessage(Action action, string expecetedMessage)
        {
            string actualMessage = "";
            try
            {
                action();
            }
            catch (Exception ex)
            {
                DocumentClientException dbcDbExp = ex as DocumentClientException;

                if (dbcDbExp == null)
                {
                    Assert.Fail($"Action did not throw DocumentClientException. Actual exception {ex.GetType()}");
                }

                if (ex.Message.Contains(expecetedMessage))
                {
                    return;
                }

                actualMessage = ex.Message;
            }

            Assert.Fail($"Action did not throw DocumentClientException expected where the message contains {expecetedMessage}, Actual exception message: {actualMessage}");
        }

        internal static void LogRequestOptions(RequestOptions options, bool shouldLogOfferThroughput = true)
        {
            string format = shouldLogOfferThroughput ?
                "RequestOptions: [AccessCondition: [Type: {0}, Condition: {1}], ConsistencyLevel: {2}, IndexingDirective: {3}, SessionToken: {4}, OfferType: {5}, OfferThroughput: {6}]" :
                "RequestOptions: [AccessCondition: [Type: {0}, Condition: {1}], ConsistencyLevel: {2}, IndexingDirective: {3}, SessionToken: {4}, OfferType: {5}]";

            Logger.LogLine(
                format,
                options.AccessCondition.Type,
                options.AccessCondition.Condition,
                options.ConsistencyLevel,
                options.IndexingDirective,
                options.SessionToken,
                options.OfferType,
                options.OfferThroughput);
        }

        private static TracerProvider OTelTracerProvider;
        private static CustomListener TestListener;
        
        internal static CustomListener ConfigureOpenTelemetryAndCustomListeners()
        {
            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

            AzureCore.ActivityExtensions.ResetFeatureSwitch();
            
            // Open Telemetry Listener
            Util.OTelTracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddCustomOtelExporter() // use any exporter here
                .AddSource($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.*")
                .Build();

            // Custom Listener
            Util.TestListener = new CustomListener($"{OpenTelemetryAttributeKeys.DiagnosticNamespace}.*", "Azure-Cosmos-Operation-Request-Diagnostics");

            return Util.TestListener;

        }

        internal static void DisposeOpenTelemetryAndCustomListeners()
        {
            // Open Telemetry Listener
            Util.OTelTracerProvider?.Dispose();

            // Custom Listener
            Util.TestListener?.Dispose();

            Util.OTelTracerProvider = null;
            Util.TestListener = null;

            AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", false);

            AzureCore.ActivityExtensions.ResetFeatureSwitch();
        }

        /// <summary>
        /// Enables traces for local debugging
        /// </summary>
        internal static void EnableTracesForDebugging()
        {
            Type defaultTrace = Type.GetType("Microsoft.Azure.Cosmos.Core.Trace.DefaultTrace,Microsoft.Azure.Cosmos.Direct");
            TraceSource traceSource = (TraceSource)defaultTrace.GetProperty("TraceSource").GetValue(null);
            traceSource.Switch.Level = SourceLevels.All;
            traceSource.Listeners.Clear();
            traceSource.Listeners.Add(new DirectToConsoleTraceListener());
        }

        public class DirectToConsoleTraceListener : TextWriterTraceListener
        {
            public DirectToConsoleTraceListener() : base(new DirectToConsoleTextWriter())
            {
            }

            public override void Close()
            {
            }
        }

        public class DirectToConsoleTextWriter : TextWriter
        {
            public override Encoding Encoding => Console.Out.Encoding;

            public override void Write(string value)
            {
                Logger.LogLine(value);
            }

            public override void WriteLine(string value)
            {
                Logger.LogLine(value);
            }
        }
    }
}
