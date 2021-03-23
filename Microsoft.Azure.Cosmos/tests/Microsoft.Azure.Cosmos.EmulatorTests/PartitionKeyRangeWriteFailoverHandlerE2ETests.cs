//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class PartitionKeyRangeWriteFailoverHandlerE2ETests : BaseCosmosClientHelper
    {
        private static readonly string InvalidReadRegionName = "DoesNotExistsEndpoint";
        private Container Container = null;
        private readonly RegionHelper InvalidWriteRegion = new RegionHelper("InvalidMockWriteRegion", $"https://127.0.0.1-{InvalidReadRegionName}:8081/");
        
        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            string PartitionKey = "/pk";
            ContainerResponse response = await this.database.CreateContainerAsync(
                new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                throughput: 20000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TestCustomPropertyWithHandler()
        {
            List<(string, string)> httpMessages = new List<(string, string)>();
            HttpClientHandlerHelper handlerHelper = new HttpClientHandlerHelper
            {
                RequestCallBack = (httpRequest, cancellationToken) =>
                {
                    if (httpRequest.RequestUri.ToString().Contains(InvalidReadRegionName))
                    {
                        throw new HttpRequestException("MockGatewayDownFailure");
                    }

                    return null;
                },
                ResponseCallBack = (httpRequestMessage, responseMessage) =>
                {
                    string request = JsonConvert.SerializeObject(httpRequestMessage);
                    JObject response = JObject.FromObject(responseMessage);
                    
                    httpMessages.Add((request, response.ToString()));

                    // On the get account information call inject an invalid region to mimic gateway
                    // being down.
                    if(httpRequestMessage.RequestUri.ToString() == "https://127.0.0.1:8081/")
                    {
                        responseMessage.Content = this.InjectInvalidRegionInformation(responseMessage.Content);
                        return responseMessage;
                    }
                    
                    return null;
                }
            };

            HttpClient httpClient = new HttpClient(handlerHelper);
            CosmosClientOptions cosmosClientOptions = new CosmosClientOptions()
            {
                HttpClientFactory = () => httpClient,
                EnablePartitionLevelFailover = true,
                ConsistencyLevel = Cosmos.ConsistencyLevel.Strong,
                ApplicationPreferredRegions = new List<string>() 
                { 

                    Regions.SouthCentralUS,
                    this.InvalidWriteRegion.Name
                },
            };

            CosmosClient customClient = TestCommon.CreateCosmosClient(cosmosClientOptions);

            Container container = customClient.GetContainer(this.database.Id, this.Container.Id);

            ToDoActivity toDoActivity = ToDoActivity.CreateRandomToDoActivity();
            ItemResponse<ToDoActivity> response = await container.CreateItemAsync(toDoActivity, new Cosmos.PartitionKey(toDoActivity.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

            ToDoActivity toDoActivity2 = ToDoActivity.CreateRandomToDoActivity();
            toDoActivity2.pk = toDoActivity.pk;

            response = await container.CreateItemAsync(toDoActivity2, new Cosmos.PartitionKey(toDoActivity2.pk));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        }

        private HttpContent InjectInvalidRegionInformation(HttpContent httpContent)
        {
            string body = httpContent.ReadAsStringAsync().GetAwaiter().GetResult();
            JObject jobject = JObject.Parse(body);
            jobject["writableLocations"] = JToken.FromObject(new List<RegionHelper>() { this.InvalidWriteRegion });
            RegionHelper[] readRegions = jobject["readableLocations"].ToObject<RegionHelper[]>();
            List<RegionHelper> newReadRegionList = new List<RegionHelper>
            {
                this.InvalidWriteRegion
            };
            newReadRegionList.AddRange(readRegions);
            jobject["readableLocations"] = JToken.FromObject(newReadRegionList);
            return new StringContent(jobject.ToString());
        }

        private sealed class RegionHelper
        {
            public RegionHelper(string name, string endpoint)
            {
                this.Name = name;
                this.DatabaseAccountEndpoint = endpoint;
            }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "databaseAccountEndpoint")]
            public string DatabaseAccountEndpoint { get; set; }
        }
    }
}
