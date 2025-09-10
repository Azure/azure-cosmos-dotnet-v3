namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Tracing.TraceData.ClientSideRequestStatisticsTraceDatum;

    [TestClass]
    public class CosmosItemConsistencyTests : BaseCosmosClientHelper
    {
        HttpClientHandlerHelper httpClientHandlerHelper;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            this.httpClientHandlerHelper = new HttpClientHandlerHelper()
            {
                ResponseIntercepter = async (response, request) =>
                {
                    string json = await response?.Content?.ReadAsStringAsync();
                    if (json.Length > 0 && json.Contains("databaseAccountEndpoint"))
                    {
                        JObject parsedDatabaseAccountResponse = JObject.Parse(json);
                        if (parsedDatabaseAccountResponse.ContainsKey("enableNRegionSynchronousCommit"))
                        {
                            parsedDatabaseAccountResponse.Property("enableNRegionSynchronousCommit").Value = true.ToString();
                        }
                        else
                        {
                            parsedDatabaseAccountResponse.Add("enableNRegionSynchronousCommit", true);
                        }
                        string interceptedResponseStr = parsedDatabaseAccountResponse.ToString();
                        HttpResponseMessage interceptedResponse = new()
                        {
                            StatusCode = response.StatusCode,
                            Content = new StringContent(interceptedResponseStr),
                            Version = response.Version,
                            ReasonPhrase = response.ReasonPhrase,
                            RequestMessage = response.RequestMessage,
                        };
                        return interceptedResponse;
                    }
                    return response;
                },
            };
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestNRegionCommitEnabledThenWriteBarrier()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            int createDocumentCallCount = 0;
            Func<TransportClient, TransportClient> transportClientDelegate = (transportClient) => new TransportClientHelper.TransportClientWrapper(
                transportClient,
                null,
                null,
                (resourceOperation, storeResponse) =>
                {
                    // Intercept the StoreResponse after the transport client receives it.
                    if ((resourceOperation.ResourceType == ResourceType.Document &&
                     resourceOperation.OperationType == OperationType.Create) ||
                     (resourceOperation.ResourceType == ResourceType.Collection &&
                     resourceOperation.OperationType == OperationType.Head))
                    {
                        string lsnActualWrite = "100";
                        string lsnGlobalNRegionCommitted = createDocumentCallCount++ == 5 ? lsnActualWrite : "90";
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, lsnActualWrite);
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1");
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalCommittedLSN, "100");
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, lsnGlobalNRegionCommitted);
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "1");
                    }
                    return storeResponse;
                }
                );

            try
            {
                await base.TestInit((builder) => builder
                .WithHttpClientFactory(() => new HttpClient(this.httpClientHandlerHelper))
                .WithTransportClientHandlerFactory(transportClientDelegate));


                Container container = await this.database.CreateContainerAsync(
                    new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"));

                ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("Item with Session Nregion commit writes");

                ItemResponse<ToDoActivity> itemResponse = await container.CreateItemAsync<ToDoActivity>(
                    partitionKey: new Cosmos.PartitionKey(temp.pk),
                    item: temp);


                // Assert we made 5 HEAD requests to ensure write barrier is met
                CosmosTraceDiagnostics traceDiagnostic = itemResponse.Diagnostics as CosmosTraceDiagnostics;

                int headRequestCount = 0;

                ClientSideRequestStatisticsTraceDatum clientSideRequestStats = this.GetClientSideRequestStatsFromTrace(traceDiagnostic.Value, "Transport Request");
                foreach (StoreResponseStatistics responseStatistics in clientSideRequestStats.StoreResponseStatisticsList)
                {
                    if (responseStatistics.RequestResourceType == ResourceType.Collection &&
                        responseStatistics.RequestOperationType == OperationType.Head)
                    {
                        headRequestCount++;
                    }
                }

                Assert.AreEqual(5, headRequestCount, "Expected 5 HEAD requests to be made to ensure write barrier is met");


                Console.WriteLine(itemResponse.Diagnostics);
            }
            catch (Exception ex)
            {
                Assert.Fail("Test failed with exception: " + ex.ToString());
            }
        }

        [TestMethod]
        public async Task TestNRegionLsnNeverRecoversThenWriteFailure()
        {
            ToDoActivity testItem = ToDoActivity.CreateRandomToDoActivity();
            Func<TransportClient, TransportClient> transportClientDelegate = (transportClient) => new TransportClientHelper.TransportClientWrapper(
                transportClient,
                null,
                null,
                (resourceOperation, storeResponse) =>
                {
                    // Intercept the StoreResponse after the transport client receives it.
                    if ((resourceOperation.ResourceType == ResourceType.Document &&
                     resourceOperation.OperationType == OperationType.Create) ||
                     (resourceOperation.ResourceType == ResourceType.Collection &&
                     resourceOperation.OperationType == OperationType.Head))
                    {
                        string lsnActualWrite = "100";
                        string lsnGlobalNRegionCommitted = "90";
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.LSN, lsnActualWrite);
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.ActivityId, "ACTIVITYID1_1");
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.GlobalNRegionCommittedGLSN, lsnGlobalNRegionCommitted);
                        storeResponse.Headers.Set(WFConstants.BackendHeaders.NumberOfReadRegions, "1");
                    }
                    return storeResponse;
                }
            );
            try
            {
                await base.TestInit((builder) => builder
                .WithHttpClientFactory(() => new HttpClient(this.httpClientHandlerHelper))
                .WithTransportClientHandlerFactory(transportClientDelegate));

                Container container = await this.database.CreateContainerAsync(
                    new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/pk"));

                ToDoActivity temp = ToDoActivity.CreateRandomToDoActivity("Item with Session nregion writes");
                CosmosException cosmosException =  await Assert.ThrowsExceptionAsync<CosmosException>(() => container.CreateItemAsync<ToDoActivity>(
                    partitionKey: new Cosmos.PartitionKey(temp.pk),
                    item: temp));

                Assert.AreEqual(System.Net.HttpStatusCode.ServiceUnavailable, cosmosException.StatusCode);
            }
            catch (Exception ex)
            {
                Assert.Fail("Test failed with exception: " + ex.ToString());
            }
        }

        private ClientSideRequestStatisticsTraceDatum GetClientSideRequestStatsFromTrace(ITrace trace, string traceToFind)
        {
            if (trace.Name.Contains(traceToFind))
            {
                foreach (object datum in trace.Data.Values)
                {
                    if (datum is ClientSideRequestStatisticsTraceDatum clientSideStats)
                    {
                        return clientSideStats;
                    }
                }
            }

            foreach (ITrace child in trace.Children)
            {
                ClientSideRequestStatisticsTraceDatum datum = this.GetClientSideRequestStatsFromTrace(child, traceToFind);
                if (datum != null)
                {
                    return datum;
                }
            }
            return null;
        }

    }
}
