namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    
    using Microsoft.Azure.Cosmos;
    
    using Microsoft.Azure.Cosmos.FaultInjection;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.SDK.EmulatorTests.CosmosAvailabilityStrategyTests;
    using Container = Container;

    [TestClass]
    public class SessionRetryOptionsTest
    {
        private string connectionString;
        private IDictionary<string, System.Uri> writeRegionMap;
        
        // to run code before running each test
        [TestInitialize]
        public async Task TestInitAsync()
        {
            //this.connectionString = ConfigurationManager.GetEnvironmentVariable<string>("COSMOSDB_MULTI_REGION", null);
            if (string.IsNullOrEmpty(this.connectionString))
            {
                Assert.Fail("Set environment variable COSMOSDB_MULTI_REGION to run the tests");
            }

            CosmosClient client = new CosmosClient(this.connectionString);
            await MultiRegionSetupHelpers.GetOrCreateMultiRegionDatabaseAndContainers(client);
            this.writeRegionMap = client.DocumentClient.GlobalEndpointManager.GetAvailableWriteEndpointsByLocation();
            Assert.IsTrue(this.writeRegionMap.Count() >= 2);

        }

        [TestMethod]
        [DataRow(FaultInjectionOperationType.ReadItem, 2, true , DisplayName = "ValidateAvailabilityStrategyNoTriggerTest with preferred regions.")]
        [TestCategory("MultiMaster")]
        public async Task ReadOperationWithReadSessionUnavailableTest(FaultInjectionOperationType faultInjectionOperationType,
            int sessionTokenMismatchRetryAttempts , Boolean remoteRegionPreferred)
        {

            string[] preferredRegions = this.writeRegionMap.Keys.ToArray();
            Console.WriteLine($" preferred regions are {String.Join(" , ", preferredRegions)}");
            // if I go to South Central US for reading an item, I should get a 404/2002 response for 90 minutes
            FaultInjectionRule badSessionTokenRule = new FaultInjectionRuleBuilder(
                id: "badSessionTokenRule",
                condition:
                    new FaultInjectionConditionBuilder()
                        .WithOperationType(faultInjectionOperationType)
                        .WithRegion(preferredRegions[0])
                        .Build(),
                result:
                    FaultInjectionResultBuilder.GetResultBuilder(FaultInjectionServerErrorType.ReadSessionNotAvailable)
                        .Build())
                .WithDuration(TimeSpan.FromMinutes(10))
                .Build();

            List<FaultInjectionRule> rules = new List<FaultInjectionRule>() { badSessionTokenRule };
            FaultInjector faultInjector = new FaultInjector(rules);
            Assert.IsNotNull(faultInjector);
            //badSessionTokenRule.Disable(); // enable it back when reading an item



            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                SessionRetryOptions = new SessionRetryOptions()
                {
                    MinInRegionRetryTime = 100,
                    MaxInRegionRetryCount = sessionTokenMismatchRetryAttempts
                },
                ConsistencyLevel = ConsistencyLevel.Session,
                ApplicationPreferredRegions = preferredRegions,
                ConnectionMode = ConnectionMode.Direct,
            };

            using (CosmosClient faultInjectionClient = new CosmosClient(
                connectionString: this.connectionString,
                clientOptions: faultInjector.GetFaultInjectionClientOptions(clientOptions)))
                //clientOptions: clientOptions))
            {
                Database database = faultInjectionClient.GetDatabase(MultiRegionSetupHelpers.dbName);
                Container container = await database.CreateContainerIfNotExistsAsync("sessionRetryPolicy", "/id");
                string GUID = Guid.NewGuid().ToString();
                dynamic testObject = new
                {
                    id = GUID,
                    name = "customer one",
                    address = new
                    {
                        line1 = "45 new street",
                        city = "mckinney",
                        postalCode = "98989",
                    }

                };

                ItemResponse<dynamic> response = await container.CreateItemAsync<dynamic>(testObject);
                Assert.IsNotNull(response);
                //badSessionTokenRule.Enable();

                /*ItemResponse<dynamic> itemResponse = await container.ReadItemAsync<dynamic>(GUID,
                                                                                  new PartitionKey(GUID));
                Assert.IsNotNull(itemResponse);*/

                OperationExecutionResult executionResult = await this.PerformDocumentOperation(faultInjectionOperationType, container, GUID);
                this.ValidateOperationExecutionResult(executionResult, remoteRegionPreferred);

                // For a non-write operation, the request can go to multiple replicas (upto 4 replicas)
                // Check if the SessionTokenMismatchRetryPolicy retries on the bad / lagging region
                // for sessionTokenMismatchRetryAttempts by tracking the badSessionTokenRule hit count
                long hitCount = badSessionTokenRule.GetHitCount();
                Console.WriteLine($" hit count is {hitCount}");

                if (remoteRegionPreferred)
                {
                    Assert.IsTrue(hitCount >= sessionTokenMismatchRetryAttempts && hitCount <= (1 + sessionTokenMismatchRetryAttempts) * 4); 
                }
                
            }
        }

        private void ValidateOperationExecutionResult(OperationExecutionResult operationExecutionResult, Boolean remoteRegionPreferred)
        {
            int sessionTokenMismatchDefaultWaitTime = 5000;

            FaultInjectionOperationType executionOpType = operationExecutionResult.OperationType;
            HttpStatusCode statusCode = operationExecutionResult.StatusCode;
            int executionDuration = operationExecutionResult.Duration;

            if (executionOpType == FaultInjectionOperationType.CreateItem)
            {
                //assertThat(statusCode).isEqualTo(HttpConstants.StatusCodes.CREATED);
            }
            else if (executionOpType == FaultInjectionOperationType.DeleteItem)
            {
                //assertThat(statusCode).isEqualTo(HttpConstants.StatusCodes.NO_CONTENT);
            }
            else if (executionOpType == FaultInjectionOperationType.UpsertItem)
            {
                //assertThat(statusCode == HttpConstants.StatusCodes.OK || statusCode == HttpConstants.StatusCodes.CREATED).isTrue();
            }
            else
            {
                Assert.IsTrue(statusCode == HttpStatusCode.OK);
            }

            if (remoteRegionPreferred)
            {
                Assert.IsTrue(executionDuration < sessionTokenMismatchDefaultWaitTime);
            }
            else 
            {
                Assert.IsTrue(executionDuration > sessionTokenMismatchDefaultWaitTime);
            }

        }


        private async Task<OperationExecutionResult> PerformDocumentOperation(FaultInjectionOperationType operationType, Container container,
                                                                                    string partitionKeyValue)
        {
            
            Stopwatch durationTimer = new Stopwatch();
            if (operationType == FaultInjectionOperationType.ReadItem)
            {
                durationTimer.Start();
                ItemResponse<dynamic> itemResponse = await container.ReadItemAsync<dynamic>(partitionKeyValue, 
                                                                                  new PartitionKey(partitionKeyValue));
                durationTimer.Stop();
                int timeElapsed = Convert.ToInt32(durationTimer.Elapsed.TotalMilliseconds);

                return new OperationExecutionResult(
                    itemResponse.Diagnostics,
                    timeElapsed,
                    itemResponse.StatusCode,
                    operationType);
                
            }

            return null;
        }

    }
    
    internal class OperationExecutionResult 
    {
        public CosmosDiagnostics Diagnostics { get; set; }
        public int Duration { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public FaultInjectionOperationType OperationType { get; set; }
        
        public OperationExecutionResult(CosmosDiagnostics diagnostics, int duration, HttpStatusCode statusCode, FaultInjectionOperationType operationType)
        {
            this.Diagnostics = diagnostics;
            this.Duration = duration;
            this.StatusCode = statusCode;
            this.OperationType = operationType;
        }
    }

    internal class AvailabilityStrategyTestObject
    {

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("pk")]
        public string Pk { get; set; }

        [JsonPropertyName("other")]
        public string Other { get; set; }
    }

}
