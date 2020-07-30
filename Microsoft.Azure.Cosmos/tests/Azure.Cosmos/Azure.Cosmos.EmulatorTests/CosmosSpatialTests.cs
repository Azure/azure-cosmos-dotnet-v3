namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Net;
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosSpatialTests
    {
        private ContainerCore Container = null;
        private readonly string spatialName = "SpatialName";
        protected CancellationTokenSource cancellationTokenSource = null;
        protected CancellationToken cancellationToken;
        protected CosmosClient cosmosClient = null;
        protected CosmosDatabase database = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            this.cancellationTokenSource = new CancellationTokenSource();
            this.cancellationToken = this.cancellationTokenSource.Token;

            this.cosmosClient = TestCommon.CreateCosmosClient();
            this.database = await this.cosmosClient.CreateDatabaseAsync(Guid.NewGuid().ToString(),
                cancellationToken: this.cancellationToken);

            string PartitionKey = "/partitionKey";
            CosmosContainerResponse response = await this.database.CreateContainerAsync(
                new CosmosContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: PartitionKey),
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Value);
            this.Container = (ContainerCore)response;
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            if (this.cosmosClient == null)
            {
                return;
            }

            if (this.database != null)
            {
                await this.database.DeleteStreamAsync(
                    requestOptions: null,
                    cancellationToken: this.cancellationToken);
            }

            this.cancellationTokenSource?.Cancel();

            this.cosmosClient.Dispose();
        }

        //[TestMethod]
        //public async Task CreateDropMultiPolygonTest()
        //{
        //    SpatialItem spatialItem = new SpatialItem
        //    {
        //        Name = spatialName,
        //        partitionKey = Guid.NewGuid().ToString(),
        //        id = Guid.NewGuid().ToString(),
        //        multiPolygon = this.GetMultiPoygon(),
        //    };

        //    ItemResponse<SpatialItem> createResponse = await this.Container.CreateItemAsync<SpatialItem>(item: spatialItem);
        //    Assert.IsNotNull(createResponse);
        //    Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

        //    ItemResponse<SpatialItem> readResponse = await this.Container.ReadItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
        //    Assert.IsNotNull(readResponse);
        //    Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);
        //    Assert.IsNotNull(readResponse.Value.multiPolygon);

        //    IOrderedQueryable<SpatialItem> multipolygonQuery =
        //      this.documentClient.CreateDocumentQuery<SpatialItem>(this.Container.LinkUri.OriginalString, new FeedOptions() { EnableScanInQuery = true, EnableCrossPartitionQuery = true });
        //    SpatialItem[] withinQuery = multipolygonQuery
        //      .Where(f => f.multiPolygon.Within(this.GetMultiPoygon()) && f.multiPolygon.IsValid())
        //      .ToArray();
        //    Assert.IsTrue(withinQuery.Length == 1);
        //    foreach (SpatialItem item in withinQuery)
        //    {
        //        Assert.IsTrue(item.multiPolygon.Equals(this.GetMultiPoygon()));
        //    }

        //    ItemResponse<SpatialItem> deleteResponse = await this.Container.DeleteItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
        //    Assert.IsNotNull(deleteResponse);
        //    Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        //}

        [TestMethod]
        public async Task CreateDropPolygonTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                polygon = this.GetPolygon(),
            };

            ItemResponse<SpatialItem> createResponse = await this.Container.CreateItemAsync<SpatialItem>(item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            ItemResponse<SpatialItem> readResponse = await this.Container.ReadItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);
            Assert.IsNotNull(readResponse.Value.polygon);

            ItemResponse<SpatialItem> deleteResponse = await this.Container.DeleteItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateDropLineStringTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                lineString = this.GetLineString(),
            };

            ItemResponse<SpatialItem> createResponse = await this.Container.CreateItemAsync<SpatialItem>(item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            ItemResponse<SpatialItem> readResponse = await this.Container.ReadItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);
            Assert.IsNotNull(readResponse.Value.lineString);

            ItemResponse<SpatialItem> deleteResponse = await this.Container.DeleteItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        [TestMethod]
        public async Task CreateDropPointTest()
        {
            SpatialItem spatialItem = new SpatialItem
            {
                Name = spatialName,
                partitionKey = Guid.NewGuid().ToString(),
                id = Guid.NewGuid().ToString(),
                point = this.GetPoint(),
            };
            ItemResponse<SpatialItem> createResponse = await this.Container.CreateItemAsync<SpatialItem>(item: spatialItem);
            Assert.IsNotNull(createResponse);
            Assert.AreEqual((int)HttpStatusCode.Created, createResponse.GetRawResponse().Status);

            ItemResponse<SpatialItem> readResponse = await this.Container.ReadItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(readResponse);
            Assert.AreEqual((int)HttpStatusCode.OK, readResponse.GetRawResponse().Status);
            Assert.IsNotNull(readResponse.Value.point);

            ItemResponse<SpatialItem> deleteResponse = await this.Container.DeleteItemAsync<SpatialItem>(partitionKey: new Cosmos.PartitionKey(spatialItem.partitionKey), id: spatialItem.id);
            Assert.IsNotNull(deleteResponse);
            Assert.AreEqual((int)HttpStatusCode.NoContent, deleteResponse.GetRawResponse().Status);
        }

        internal class SpatialItem
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            public string id { get; set; }


            [JsonPropertyName("point")]
            public Point point { get; set; }

            [JsonPropertyName("polygon")]
            public Polygon polygon { get; set; }

            [JsonPropertyName("lineString")]
            public LineString lineString { get; set; }

            [JsonPropertyName("partitionKey")]
            public string partitionKey { get; set; }

            [JsonPropertyName("MultiPointLocation")]
            public MultiPolygon multiPolygon { get; set; }

        }

        private Polygon GetPolygon()
        {
            Polygon polygon = new Polygon(
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
                new BoundingBox((0, 0), (40, 40)));
            return polygon;
        }

        private LineString GetLineString()
        {
            LineString lineString = new LineString(
                new[] {
                    new Position(20, 30), new Position(30, 40) },
                    new BoundingBox((0, 0), (40, 40)));
            return lineString;
        }

        private Point GetPoint()
        {
            Point point = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 40)));
            return point;
        }
    }
}
