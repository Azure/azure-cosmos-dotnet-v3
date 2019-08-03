namespace Microsoft.Azure.Cosmos.UT.FakeItEasy
{
    using global::FakeItEasy;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class CosmosClientUT
    {
        const string DbName = "somevalue";
        const string ContainerName = "container1";
        const string Query = "select * from c";
        static readonly QueryDefinition QueryDef = new QueryDefinition("select * from c");
        const string ContinuationToken = "{CONTINUATION_TOKEN}";

        [TestMethod]
        public async Task CreateDatabaseAsyncTests()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            DatabaseResponse fakeDbResponse = A.Fake<DatabaseResponse>();

            A.CallTo(() => fakeClient.CreateDatabaseAsync(A<string>.Ignored, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeDbResponse);

            DatabaseResponse dbResponse = await fakeClient.CreateDatabaseAsync(CosmosClientUT.DbName);

            Assert.ReferenceEquals(fakeDbResponse, dbResponse);
            A.CallTo(() => fakeClient.CreateDatabaseAsync(CosmosClientUT.DbName, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CreateDatabaseIfNotExistsAsync()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            DatabaseResponse fakeDbResponse = A.Fake<DatabaseResponse>();

            A.CallTo(() => fakeClient.CreateDatabaseIfNotExistsAsync(A<string>.Ignored, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeDbResponse);

            DatabaseResponse dbResponse = await fakeClient.CreateDatabaseIfNotExistsAsync(CosmosClientUT.DbName);

            Assert.ReferenceEquals(fakeDbResponse, dbResponse);
            A.CallTo(() => fakeClient.CreateDatabaseIfNotExistsAsync(CosmosClientUT.DbName, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CreateDatabaseStreamAsync()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            ResponseMessage fakeDbResponse = A.Fake<ResponseMessage>();

            A.CallTo(() => fakeClient.CreateDatabaseStreamAsync(A<DatabaseProperties>.Ignored, A<int?>.Ignored, A<RequestOptions>.Ignored, A<CancellationToken>.Ignored))
                .Returns(fakeDbResponse);

            DatabaseProperties databaseProperties = new DatabaseProperties(CosmosClientUT.DbName);
            ResponseMessage dbResponse = await fakeClient.CreateDatabaseStreamAsync(databaseProperties);

            Assert.ReferenceEquals(fakeDbResponse, dbResponse);
            A.CallTo(() => fakeClient.CreateDatabaseStreamAsync(
                    A<DatabaseProperties>.That.Matches(e => e.Id == CosmosClientUT.DbName), 
                    A<int?>.Ignored, 
                    A<RequestOptions>.Ignored, 
                    A<CancellationToken>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetDatabase()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            Database fakeDb = A.Fake<Database>();

            A.CallTo(() => fakeClient.GetDatabase(A<string>.Ignored))
                .Returns(fakeDb);

            Database db = fakeClient.GetDatabase(CosmosClientUT.DbName);
            Assert.ReferenceEquals(fakeDb, db);

            A.CallTo(() => fakeClient.GetDatabase(CosmosClientUT.DbName))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetContainer()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            Container fakeContainer = A.Fake<Container>();

            A.CallTo(() => fakeClient.GetContainer(A<string>.Ignored, A<string>.Ignored))
                .Returns(fakeContainer);

            Container container = fakeClient.GetContainer(CosmosClientUT.DbName, CosmosClientUT.ContainerName);

            Assert.ReferenceEquals(fakeContainer, container);
            A.CallTo(() => fakeClient.GetContainer(CosmosClientUT.DbName, CosmosClientUT.ContainerName))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetDatabaseQueryIterator()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            FeedIterator<DatabaseProperties> fakeIterator = A.Fake<FeedIterator<DatabaseProperties>>();

            A.CallTo(() => fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeIterator);

            FeedIterator<DatabaseProperties> iterator = fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(CosmosClientUT.Query, CosmosClientUT.ContinuationToken);

            Assert.ReferenceEquals(fakeIterator, iterator);
            A.CallTo(() => fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(CosmosClientUT.Query, CosmosClientUT.ContinuationToken, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetDatabaseQueryIteratorWithDefinition()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            FeedIterator<DatabaseProperties> fakeIterator = A.Fake<FeedIterator<DatabaseProperties>>();

            A.CallTo(() => fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeIterator);

            FeedIterator<DatabaseProperties> iterator = fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(CosmosClientUT.QueryDef, CosmosClientUT.ContinuationToken);

            Assert.ReferenceEquals(fakeIterator, iterator);
            A.CallTo(() => fakeClient.GetDatabaseQueryIterator<DatabaseProperties>(CosmosClientUT.QueryDef, CosmosClientUT.ContinuationToken, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetDatabaseQueryStreamIterator()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            FeedIterator fakeIterator = A.Fake<FeedIterator>();

            A.CallTo(() => fakeClient.GetDatabaseQueryStreamIterator(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeIterator);

            FeedIterator iterator = fakeClient.GetDatabaseQueryStreamIterator(CosmosClientUT.Query, CosmosClientUT.ContinuationToken);

            Assert.ReferenceEquals(fakeIterator, iterator);
            A.CallTo(() => fakeClient.GetDatabaseQueryStreamIterator(CosmosClientUT.Query, CosmosClientUT.ContinuationToken, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetDatabaseQueryStreamIteratorWithDefinition()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            FeedIterator fakeIterator = A.Fake<FeedIterator>();

            A.CallTo(() => fakeClient.GetDatabaseQueryStreamIterator(A<string>.Ignored, A<string>.Ignored, A<QueryRequestOptions>.Ignored))
                .Returns(fakeIterator);

            FeedIterator iterator = fakeClient.GetDatabaseQueryStreamIterator(CosmosClientUT.QueryDef, CosmosClientUT.ContinuationToken);

            Assert.ReferenceEquals(fakeIterator, iterator);
            A.CallTo(() => fakeClient.GetDatabaseQueryStreamIterator(CosmosClientUT.QueryDef, CosmosClientUT.ContinuationToken, A<QueryRequestOptions>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetClientOptions()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            CosmosClientOptions clientOptions = new CosmosClientOptions()
            {
                ApplicationName = "TestApp",
                ApplicationRegion = Regions.EastUS2,
            };


            A.CallTo(() => fakeClient.ClientOptions)
                .Returns(clientOptions);

            Assert.ReferenceEquals(clientOptions, fakeClient.ClientOptions);
            A.CallTo(() => fakeClient.ClientOptions)
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void GetEndpoint()
        {
            CosmosClient fakeClient = A.Fake<CosmosClient>();
            Uri endPoint = new Uri("https://accountname.Cosmos.Azure.com");

            A.CallTo(() => fakeClient.Endpoint)
                .Returns(endPoint);

            Assert.ReferenceEquals(endPoint, fakeClient.Endpoint);
            A.CallTo(() => fakeClient.Endpoint)
                .MustHaveHappenedOnceExactly();
        }
    }
}
