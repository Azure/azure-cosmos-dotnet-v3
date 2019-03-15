namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading.Tasks;

    [TestClass]
    public class CosmosSpatialTests : BaseCosmosClientHelper
    {
        private CosmosContainer Container = null;
        private DocumentClient client;
        private CosmosDefaultJsonSerializer jsonSerializer = null;
        private readonly string spatialName = "spatialName";

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            this.client = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session);
            string PartitionKey = "/partitionKey";
            CosmosContainerResponse response = await this.database.Containers.CreateContainerAsync(
                new CosmosContainerSettings(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = response;
            this.jsonSerializer = new CosmosDefaultJsonSerializer();
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task CreateDropMultiPolygonTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                multiPolygon = GetMultiPoygon(),
            };
            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.multiPolygon);

            IDocumentQuery<SpatialItem> spatialQuery = this.client.CreateDocumentQuery<SpatialItem>(this.Container.Link, "select * from root", new FeedOptions() { EnableCrossPartitionQuery = true }).AsDocumentQuery(); ;
            FeedResponse<SpatialItem> queryResponse = spatialQuery.ExecuteNextAsync<SpatialItem>().GetAwaiter().GetResult();
            Assert.IsTrue(queryResponse.Count == 1);
            foreach (var item in queryResponse)
            {
                Assert.IsTrue(item.multiPolygon.Equals(GetMultiPoygon()));
            }

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropPolygonTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                polygon = GetPolygon(),
            };
            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.polygon);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropLineStringTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                lineString = getLineString(),
            };
            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.lineString);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        [TestMethod]
        public async Task CreateDropPointTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                point = GetPoint(),
            };
            CosmosItemResponse<SpatialItem> createResponse = await this.Container.Items.CreateItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);

            CosmosItemResponse<SpatialItem> readResponse = await this.Container.Items.ReadItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.IsNotNull(readResponse.Resource.point);

            CosmosItemResponse<SpatialItem> deleteResponse = await this.Container.Items.DeleteItemAsync<SpatialItem>(partitionKey: spatialItem.partitionKey, id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        }

        public class SpatialItem
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            public string id { get; set; }


            [JsonProperty("point")]
            public Point point { get; set; }

            [JsonProperty("polygon")]
            public Polygon polygon { get; set; }

            [JsonProperty("lineString")]
            public LineString lineString { get; set; }

            [JsonProperty("partitionKey")]
            public string partitionKey { get; set; }

            [JsonProperty("MultiPointLocation")]
            public MultiPolygon multiPolygon { get; set; }

        }

        private MultiPolygon GetMultiPoygon()
        {
            MultiPolygon multiPolygon =
           new MultiPolygon(
           new[]
               {
                            new PolygonCoordinates(
                                new[]
                                    {
                                        new LinearRing(
                                            new[]
                                                {
                                                    new Position(20, 20), new Position(20, 21), new Position(21, 21),
                                                    new Position(21, 20), new Position(22, 20)
                                                })
                                    })
               },
                           new GeometryParams
                           {
                               AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                               BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                               Crs = Crs.Named("SomeCrs")
                           });

            return multiPolygon;
        }

        private Polygon GetPolygon()
        {
            var polygon = new Polygon(
                          new[]
        {
                        new LinearRing(
                            new[]
                                {
                                    new Position(20, 20),
                                    new Position(20, 21),
                                    new Position(21, 21),
                                    new Position(21, 20),
                                    new Position(22, 20)
                                })
        },
                          new GeometryParams
                          {
                              AdditionalProperties = new Dictionary<string, object> { { "b", "c" } },
                              BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                              Crs = Crs.Named("SomeCrs")
                          });
            return polygon;
        }

        private LineString getLineString()
        {
            var lineString = new LineString(
                             new[] {
                                 new Position(20, 30), new Position(30, 40) },
                                 new GeometryParams
                                 {
                                     AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                                     BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                                     Crs = Crs.Named("SomeCrs")
                                 });
            return lineString;
        }

        private Point GetPoint()
        {
            var point = new Point(
                        new Position(20, 30),
                        new GeometryParams
                        {
                            AdditionalProperties = new Dictionary<string, object> { { "a", "b" } },
                            BoundingBox = new BoundingBox(new Position(0, 0), new Position(40, 40)),
                            Crs = Crs.Named("SomeCrs")
                        });
            return point;
        }
    }
}
