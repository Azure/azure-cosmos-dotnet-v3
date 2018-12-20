//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Cosmos.Collections;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class GlobalDatabaseAccountTests
    {
        private static TimeSpan WaitDurationForAsyncReplication = TimeSpan.FromSeconds(60);
        private Uri writeRegionEndpointUri;
        private string masterKey;

        private const string GlobalDatabaseAccountName = "globaldb";

        [TestInitialize]
        public void TestInitialize()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            TestCommon.DeleteAllDatabasesAsync(client).Wait();

            this.writeRegionEndpointUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            this.masterKey = ConfigurationManager.AppSettings["MasterKey"];
        }

        [TestMethod]
        public void TestClientWithPreferredRegion()
        {
            TestClientWithPreferredRegionAsync().Wait();
        }

        private async Task TestClientWithPreferredRegionAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Uri readRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            // #1. Enable failover on client side, verify write succeed on readRegion.
            ConnectionPolicy failoverPolicy = new ConnectionPolicy();
            failoverPolicy.PreferredLocations.Add("West US");

            DocumentClient client3 = new DocumentClient(
                readRegionUri,
                authKey,
                failoverPolicy);

            // write should succeed as it will automatic endpoint discovery
            CosmosDatabaseSettings database = await client3.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() });
            CosmosContainerSettings collection = await client3.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings { Id = Guid.NewGuid().ToString() });

            // make sure it is replicated
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            CosmosContainerSettings collection1 = await client3.ReadDocumentCollectionAsync(collection.SelfLink);
            Document document1 = await client3.CreateDocumentAsync(collection.AltLink, new Document { Id = Guid.NewGuid().ToString() });

            // #2. Add the preferred read region. Read should go to read region.
            // make sure it is replicated
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);
            ResourceResponse<Document> response1 = await client3.ReadDocumentAsync(document1);
            Uri uri = new Uri(response1.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(8081, uri.Port, "Read should go to port 8081");

            failoverPolicy.PreferredLocations.Clear();
            failoverPolicy.PreferredLocations.Add("South Central US");

            ResourceResponse<Document> response2 = await client3.ReadDocumentAsync(document1);
            Uri uri2 = new Uri(response2.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(8081, uri2.Port, "Read should go to port 8081");

            // #3. No preferred read region. Read should go to the write region.
            failoverPolicy.PreferredLocations.Clear();

            ResourceResponse<Document> response3 = await client3.ReadDocumentAsync(document1);
            Uri uri3 = new Uri(response3.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(8081, uri3.Port, "Read should go to port 8081");

        }

        [TestMethod]
        public void TestPreferredRegionOrder()
        {
            TestPreferredRegionOrderAsync().Wait();
        }

        private async Task TestPreferredRegionOrderAsync()
        {
            Uri globalEndpointUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add(ConfigurationManager.AppSettings["Location"]);  //write region
            connectionPolicy.PreferredLocations.Add(ConfigurationManager.AppSettings["Location2"]); // read region

            DocumentClient client = new DocumentClient(globalEndpointUri, authKey, connectionPolicy);

            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() });
            CosmosContainerSettings collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings { Id = Guid.NewGuid().ToString() });

            // todo: SessionToken container has a bug which prevent the session consistency read. So we sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            Document document =
                await client.CreateDocumentAsync(collection.SelfLink, new Document { Id = Guid.NewGuid().ToString() });

            Assert.AreEqual(client.WriteEndpoint, DNSHelper.GetResolvedUri(ConfigurationManager.AppSettings["GatewayEndpoint"]));
            // Ensure that the ReadEndpoint gets set to whatever is the first region in PreferredLocations irrespective whether it's read or write region
            Assert.AreEqual(client.ReadEndpoint, DNSHelper.GetResolvedUri(ConfigurationManager.AppSettings["GatewayEndpoint"]));
        }

        [TestMethod]
        public void TestDocumentClientMemoryLeakDirectTCP()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp
            };


            this.TestDocumentClientMemoryLeakPrivate(connectionPolicy);
        }

        [TestMethod]
        public void TestDocumentClientMemoryLeakDirectHttps()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Https
            };


            this.TestDocumentClientMemoryLeakPrivate(connectionPolicy);
        }

        [TestMethod]
        public void TestDocumentClientMemoryLeakGatewayHttps()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConnectionProtocol = Protocol.Https
            };


            this.TestDocumentClientMemoryLeakPrivate(connectionPolicy);
        }


        private void TestDocumentClientMemoryLeakPrivate(ConnectionPolicy connectionPolicy)
        {
            WeakReference reference = new WeakReference(null);

            TestDocumentClientMemoryLeakPrivateNoInline(connectionPolicy, reference);

            // Forcing the GC to run and garbage collect client instance memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Verify that the client instance(target for this reference) is not alive any more
            Assert.IsTrue(!reference.IsAlive, "Memory leak");
        }

        /// <summary>
        /// A helper method that will not inline to its caller to ensure JIT-preserved locals don't interfere with weak reference tests.
        /// </summary>
        /// <remarks>
        /// NetCore 2 changed the behavior of Weakreference and GC.Collect in Debug.
        /// See: https://github.com/dotnet/coreclr/issues/12847, https://github.com/dotnet/roslyn/issues/26587, https://github.com/dotnet/coreclr/issues/13490
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TestDocumentClientMemoryLeakPrivateNoInline(ConnectionPolicy connectionPolicy, WeakReference reference)
        {
            Uri globalEndpointUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Uri configurationEndPoint = DNSHelper.GetResolvedUri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            DocumentClient client = new DocumentClient(globalEndpointUri, authKey, connectionPolicy);

            // Holding a WeakReference to client to test whether it gets garbage collected eventually
            reference.Target = client;

            // Executing any request using this client
            client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() }).Wait();

            // Verify that the Write and Read Endpoints point to same endpoint(since no PreferredLocations was specified)
            Assert.AreEqual(client.WriteEndpoint, configurationEndPoint);
            Assert.AreEqual(client.ReadEndpoint, configurationEndPoint);

            // Adding a preferred read location, which should trigger the event handler to update the Read and Write endpoints
            connectionPolicy.PreferredLocations.Add(ConfigurationManager.AppSettings["Location2"]);

            client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() }).Wait();

            // Verify that the read endpoint now changes to this new preferred location
            Assert.AreEqual(client.WriteEndpoint, configurationEndPoint);
            Assert.AreEqual(client.ReadEndpoint, configurationEndPoint);

            // Disposing the client and setting it to null to enable garbage collection
            client.Dispose();
            client = null;
        }

        [TestMethod]
        public void ReadDocumentFromReadRegionWithRetry()
        {
            ReadDocumentFromReadRegionWithRetryAsync().Wait();
        }

        [TestMethod]
        public void ValidateGetDatabaseAccountFromGateway()
        {
            ValidateGetDatabaseAccountFromGatewayAsync().Wait();
        }

        private async Task ValidateGetDatabaseAccountFromGatewayAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            using (DocumentClient client = new DocumentClient(writeRegionUri, authKey, connectionPolicy: null))
            {
                CosmosAccountSettings databaseAccount = await client.GetDatabaseAccountAsync();
                Assert.AreEqual(1, databaseAccount.WriteLocationsInternal.Count);
                Assert.AreEqual(1, databaseAccount.ReadLocationsInternal.Count);
            }
        }

        [TestMethod]
        public async Task RetryOnReadSessionNotAvailableMockTestAsync()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                PreferredLocations = { "West US" },
            };

            DocumentClient client = new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                connectionPolicy,
                ConsistencyLevel.Session);

            await client.GetDatabaseAccountAsync();

            // Set up the mock to throw exception on first call, test retry happens and request succeeds.
            Mock<IStoreModel> mockStoreModel = new Mock<IStoreModel>();
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Returns<DocumentServiceRequest, CancellationToken>((r, cancellationToken) => this.ProcessMessageForRead(client, r));

            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;

            ResourceResponse<CosmosDatabaseSettings> dbResponse = await client.ReadDatabaseAsync("/dbs/id1");
            Assert.IsNotNull(dbResponse);

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(2));

            // Set up the mock to always throw exception, test retry happens only twice and request fails.
            mockStoreModel = new Mock<IStoreModel>();
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Throws(this.CreateReadSessionNotAvailableException());

            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;

            bool failed = false;
            try
            {
                dbResponse = await client.ReadDatabaseAsync("/dbs/id1");
                Assert.IsNull(dbResponse);
            }
            catch (DocumentClientException e)
            {
                failed = true;
                Assert.AreEqual(HttpStatusCode.NotFound, e.StatusCode);
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(2));
            Assert.IsTrue(failed);

            failed = false;
            try
            {
                IQueryable<dynamic> dbIdQuery = client.CreateDatabaseQuery(@"select * from root r").AsQueryable();
                Assert.AreEqual(0, dbIdQuery.AsEnumerable().Count());
            }
            catch (AggregateException e)
            {
                DocumentClientException docExp = e.InnerExceptions[0] as DocumentClientException;
                Assert.IsNotNull(docExp);
                Assert.AreEqual(HttpStatusCode.NotFound, docExp.StatusCode);
                failed = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(4));
            Assert.IsTrue(failed);

            failed = false;
            try
            {
                ResourceFeedReader<CosmosDatabaseSettings> dbFeed = client.CreateDatabaseFeedReader();
                FeedResponse<CosmosDatabaseSettings> response = await dbFeed.ExecuteNextAsync();
                Assert.AreEqual(1, response.Count);
                Assert.AreEqual(false, dbFeed.HasMoreResults);
            }
            catch (DocumentClientException docExp)
            {
                Assert.IsNotNull(docExp);
                Assert.AreEqual(HttpStatusCode.NotFound, docExp.StatusCode);
                failed = true;
            }

            mockStoreModel.Verify(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)), Times.Exactly(6));
            Assert.IsTrue(failed);
        }
        private async Task<DocumentServiceResponse> ProcessMessageForRead(DocumentClient client, DocumentServiceRequest request)
        {
            Uri requestEndpointUri = client.GlobalEndpointManager.ResolveServiceEndpoint(request);
            Assert.IsNotNull(requestEndpointUri);

            PartitionAddressInformation partitionAddress = await client.AddressResolver.ResolveAsync(request, false, new CancellationToken());
            AddressInformation[] addressList = partitionAddress.AllAddresses;
            Assert.IsNotNull(addressList);
            Assert.IsTrue(addressList.Length > 0);

            if (!request.ClearSessionTokenOnSessionReadFailure)
            {
                Assert.AreEqual(8081, requestEndpointUri.Port);
                throw this.CreateReadSessionNotAvailableException();
            }

            Assert.AreEqual(8081, requestEndpointUri.Port);
            return await this.CreateEmptyDocumentServiceResponse();
        }

        private DocumentClientException CreateReadSessionNotAvailableException()
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage();
            responseMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            responseMessage.Headers.Add(WFConstants.BackendHeaders.SubStatus, 1002.ToString(CultureInfo.InvariantCulture));

            Error error = new Error() { Code = "404", Message = "Message: {'Errors':['The read session is not available for the input session token.']}" };

            return new DocumentClientException(error, responseMessage.Headers, (HttpStatusCode)404);
        }

        private Task<DocumentServiceResponse> CreateEmptyDocumentServiceResponse()
        {
            var tcs = new TaskCompletionSource<DocumentServiceResponse>();
            Task.Run(
                () =>
                {
                    Task.Delay(100);
                    tcs.SetResult(new DocumentServiceResponse(new MemoryStream(), new StringKeyValueCollection(), HttpStatusCode.OK));
                });

            return tcs.Task;
        }

        private async Task ValidateCollectionCRUDAsync()
        {
            using (DocumentClient writeRegionClient = TestCommon.CreateClient(true, createForGeoRegion: false))
            {
                using (DocumentClient readRegionClient = TestCommon.CreateClient(true, createForGeoRegion: true, enableEndpointDiscovery: false))
                {
                    string databaseName = "geocollcruddb-" + Guid.NewGuid();
                    CosmosDatabaseSettings db = new CosmosDatabaseSettings
                    {
                        Id = databaseName,
                    };

                    ResourceResponse<CosmosDatabaseSettings> dbResponse = await writeRegionClient.CreateDatabaseAsync(db);
                    Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                    this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                    string databaseSelfLink = dbResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<CosmosDatabaseSettings> readRegionDbResponse = await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                    this.ValidateDatabaseResponseBody(readRegionDbResponse.Resource, databaseName);
                    Assert.AreEqual(dbResponse.Resource.ETag, readRegionDbResponse.Resource.ETag);
                    Assert.AreEqual(dbResponse.Resource.ResourceId, readRegionDbResponse.Resource.ResourceId);
                    Assert.AreEqual(dbResponse.Resource.SelfLink, readRegionDbResponse.Resource.SelfLink);

                    string collectionName = "geocollcrudcoll-" + Guid.NewGuid();
                    CosmosContainerSettings collection = new CosmosContainerSettings()
                    {
                        Id = collectionName,
                    };
                    ResourceResponse<CosmosContainerSettings> collResponse = await writeRegionClient.CreateDocumentCollectionAsync(databaseSelfLink, collection, new RequestOptions { OfferThroughput = 8000 });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(collResponse.Headers);

                    string collectionSelfLink = collResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<CosmosContainerSettings> readRegionCollResponse = await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink);
                    Assert.AreEqual(collectionName, readRegionCollResponse.Resource.Id);
                    Assert.AreEqual(collResponse.Resource.ETag, readRegionCollResponse.Resource.ETag);
                    Assert.AreEqual(collResponse.Resource.ResourceId, readRegionCollResponse.Resource.ResourceId);
                    Assert.AreEqual(collResponse.Resource.SelfLink, readRegionCollResponse.Resource.SelfLink);

                    // Create a document
                    string documentName = "geocollcruddoc-" + Guid.NewGuid();
                    Document document = new Document()
                    {
                        Id = documentName,
                    };
                    ResourceResponse<Document> docResponse = await writeRegionClient.CreateDocumentAsync(collectionSelfLink, document);
                    Assert.AreEqual(HttpStatusCode.Created, docResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(docResponse.Headers);

                    string documentSelfLink = docResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<Document> readRegionDocResponse = await readRegionClient.ReadDocumentAsync(documentSelfLink);
                    Assert.AreEqual(documentName, readRegionDocResponse.Resource.Id);
                    Assert.AreEqual(docResponse.Resource.ETag, readRegionDocResponse.Resource.ETag);
                    Assert.AreEqual(docResponse.Resource.ResourceId, readRegionDocResponse.Resource.ResourceId);
                    Assert.AreEqual(docResponse.Resource.SelfLink, readRegionDocResponse.Resource.SelfLink);

                    // Delete document from write region
                    await writeRegionClient.DeleteDocumentAsync(documentSelfLink);

                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading document from read region
                    try
                    {
                        await readRegionClient.ReadDocumentAsync(documentSelfLink);
                        Assert.Fail("Expected exception when reading deleted document from read region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    // Create another document
                    documentName = "geocolldeltestdoc-" + Guid.NewGuid();
                    document = new Document()
                    {
                        Id = documentName,
                    };
                    docResponse = await writeRegionClient.CreateDocumentAsync(collectionSelfLink, document);
                    Assert.AreEqual(HttpStatusCode.Created, docResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(docResponse.Headers);

                    documentSelfLink = docResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    readRegionDocResponse = await readRegionClient.ReadDocumentAsync(documentSelfLink);
                    Assert.AreEqual(documentName, readRegionDocResponse.Resource.Id);
                    Assert.AreEqual(docResponse.Resource.ETag, readRegionDocResponse.Resource.ETag);
                    Assert.AreEqual(docResponse.Resource.ResourceId, readRegionDocResponse.Resource.ResourceId);
                    Assert.AreEqual(docResponse.Resource.SelfLink, readRegionDocResponse.Resource.SelfLink);


                    ResourceResponse<CosmosContainerSettings> replaceCollResponse = await writeRegionClient.ReplaceDocumentCollectionAsync(collResponse.Resource);
                    Offer offer = (await writeRegionClient.ReadOffersFeedAsync()).Single(o => o.ResourceLink == collResponse.Resource.SelfLink);
                    offer = new OfferV2(offer, 1000);

                    ResourceResponse<Offer> replaceOfferResponse = await writeRegionClient.ReplaceOfferAsync(offer);

                    // TODO: Collection delete is not working, and that is why if you have this test as the last test, it will cause problem to other tests 
                    // Delete collection from write region
                    await writeRegionClient.DeleteDocumentCollectionAsync(collectionSelfLink);

                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading collection from write region
                    try
                    {
                        await writeRegionClient.ReadDocumentCollectionAsync(collectionSelfLink);
                        Assert.Fail("Expected exception when reading deleted collection from write region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    //TODO: Aditya, Investigate
                    // Try reading collection from read region
                    //try
                    //{
                    //    await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink);
                    //    Assert.Fail("Expected exception when reading deleted collection from read region");
                    //}
                    //catch (DocumentClientException clientException)
                    //{
                    //    TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    //}

                    // Now try reading the document from both the regions
                    try
                    {
                        await writeRegionClient.ReadDocumentAsync(documentSelfLink);
                        Assert.Fail("Expected exception when reading document of a deleted collection from write region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    //TODO: Aditya, Investigate
                    //try
                    //{
                    //    await readRegionClient.ReadDocumentAsync(documentSelfLink);
                    //    Assert.Fail("Expected exception when reading document of a deleted collection from read region");
                    //}
                    //catch (DocumentClientException clientException)
                    //{
                    //    TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    //}

                    // Delete the database
                    await writeRegionClient.DeleteDatabaseAsync(databaseSelfLink);

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading from read region
                    try
                    {
                        await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                        Assert.Fail("Expected exception when reading deleted database from read region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }
                }
            }
        }

        private async Task ValidatePartitionedCollectionCRUDAsync()
        {
            using (DocumentClient writeRegionClient = TestCommon.CreateClient(true, createForGeoRegion: false))
            {
                using (DocumentClient readRegionClient = TestCommon.CreateClient(true, defaultConsistencyLevel: ConsistencyLevel.Session, createForGeoRegion: true, enableEndpointDiscovery: false))
                {
                    string databaseName = "geocollcruddb-" + Guid.NewGuid();
                    CosmosDatabaseSettings db = new CosmosDatabaseSettings
                    {
                        Id = databaseName,
                    };

                    ResourceResponse<CosmosDatabaseSettings> dbResponse = await writeRegionClient.CreateDatabaseAsync(db);
                    Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                    this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                    string databaseSelfLink = dbResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<CosmosDatabaseSettings> readRegionDbResponse = await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                    this.ValidateDatabaseResponseBody(readRegionDbResponse.Resource, databaseName);
                    Assert.AreEqual(dbResponse.Resource.ETag, readRegionDbResponse.Resource.ETag);
                    Assert.AreEqual(dbResponse.Resource.ResourceId, readRegionDbResponse.Resource.ResourceId);
                    Assert.AreEqual(dbResponse.Resource.SelfLink, readRegionDbResponse.Resource.SelfLink);

                    string collectionName = "geocollcrudcoll-" + Guid.NewGuid();
                    CosmosContainerSettings collection = new CosmosContainerSettings()
                    {
                        Id = collectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/id" }
                        }
                    };
                    ResourceResponse<CosmosContainerSettings> collResponse = await writeRegionClient.CreateDocumentCollectionAsync(databaseSelfLink, collection, new RequestOptions { OfferThroughput = 12000 });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode, "Status code should be Created (201)");

                    string collectionSelfLink = collResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<CosmosContainerSettings> readRegionCollResponse = await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink, new RequestOptions { SessionToken = collResponse.SessionToken });
                    Assert.AreEqual(collectionName, readRegionCollResponse.Resource.Id);
                    Assert.AreEqual(collResponse.Resource.ETag, readRegionCollResponse.Resource.ETag);
                    Assert.AreEqual(collResponse.Resource.ResourceId, readRegionCollResponse.Resource.ResourceId);
                    Assert.AreEqual(collResponse.Resource.SelfLink, readRegionCollResponse.Resource.SelfLink);

                    // Create a document
                    string documentName = "geocollcruddoc-" + Guid.NewGuid();
                    Document document = new Document()
                    {
                        Id = documentName,
                    };
                    ResourceResponse<Document> docResponse = await writeRegionClient.CreateDocumentAsync(collectionSelfLink, document);
                    Assert.AreEqual(HttpStatusCode.Created, docResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(docResponse.Headers);

                    string documentSelfLink = docResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<Document> readRegionDocResponse = await readRegionClient.ReadDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id), SessionToken = docResponse.SessionToken });
                    Assert.AreEqual(documentName, readRegionDocResponse.Resource.Id);
                    Assert.AreEqual(docResponse.Resource.ETag, readRegionDocResponse.Resource.ETag);
                    Assert.AreEqual(docResponse.Resource.ResourceId, readRegionDocResponse.Resource.ResourceId);
                    Assert.AreEqual(docResponse.Resource.SelfLink, readRegionDocResponse.Resource.SelfLink);

                    // Delete document from write region
                    await writeRegionClient.DeleteDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id) });

                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading document from read region
                    try
                    {
                        await readRegionClient.ReadDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id), SessionToken = docResponse.SessionToken });
                        Assert.Fail("Expected exception when reading deleted document from read region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    // Create another document
                    documentName = "geocolldeltestdoc-" + Guid.NewGuid();
                    document = new Document()
                    {
                        Id = documentName,
                    };
                    docResponse = await writeRegionClient.CreateDocumentAsync(collectionSelfLink, document);
                    Assert.AreEqual(HttpStatusCode.Created, docResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(docResponse.Headers);

                    documentSelfLink = docResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    readRegionDocResponse = await readRegionClient.ReadDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id), SessionToken = docResponse.SessionToken });
                    Assert.AreEqual(documentName, readRegionDocResponse.Resource.Id);
                    Assert.AreEqual(docResponse.Resource.ETag, readRegionDocResponse.Resource.ETag);
                    Assert.AreEqual(docResponse.Resource.ResourceId, readRegionDocResponse.Resource.ResourceId);
                    Assert.AreEqual(docResponse.Resource.SelfLink, readRegionDocResponse.Resource.SelfLink);


                    ResourceResponse<CosmosContainerSettings> replaceCollResponse = await writeRegionClient.ReplaceDocumentCollectionAsync(collResponse.Resource);

                    // TODO: Collection delete is not working, and that is why if you have this test as the last test, it will cause problem to other tests 
                    // Delete collection from write region
                    await writeRegionClient.DeleteDocumentCollectionAsync(collectionSelfLink);

                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading collection from write region
                    try
                    {
                        await writeRegionClient.ReadDocumentCollectionAsync(collectionSelfLink, new RequestOptions { SessionToken = replaceCollResponse.SessionToken });
                        Assert.Fail("Expected exception when reading deleted collection from write region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    //TODO: Aditya, Investigate
                    // Try reading collection from read region
                    //try
                    //{
                    //    await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink, new RequestOptions { SessionToken = replaceCollResponse.SessionToken });
                    //    Assert.Fail("Expected exception when reading deleted collection from read region");
                    //}
                    //catch (DocumentClientException clientException)
                    //{
                    //    TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    //}

                    // Now try reading the document from both the regions
                    try
                    {
                        await writeRegionClient.ReadDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id), SessionToken = docResponse.SessionToken });
                        Assert.Fail("Expected exception when reading document of a deleted collection from write region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }

                    //TODO: Aditya, Investigate
                    //try
                    //{
                    //    await readRegionClient.ReadDocumentAsync(documentSelfLink, new RequestOptions { PartitionKey = new PartitionKey(document.Id), SessionToken = docResponse.SessionToken });
                    //    Assert.Fail("Expected exception when reading document of a deleted collection from read region");
                    //}
                    //catch (DocumentClientException clientException)
                    //{
                    //    TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    //}

                    // Delete the database
                    await writeRegionClient.DeleteDatabaseAsync(databaseSelfLink);

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    // Try reading from read region
                    try
                    {
                        await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                        Assert.Fail("Expected exception when reading deleted database from read region");
                    }
                    catch (DocumentClientException clientException)
                    {
                        TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                    }
                }
            }
        }

        private async Task ReadDocumentFromReadRegionWithRetryAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            // #1. Enable failover on client side, verify write succeed on readRegion.
            ConnectionPolicy connectionPolicy = new ConnectionPolicy();
            connectionPolicy.PreferredLocations.Add("West US");

            DocumentClient client = new DocumentClient(
                writeRegionUri,
                authKey,
                connectionPolicy,
                ConsistencyLevel.Session);

            string databaseId = "GlobalDB_SessionRetry";
            string collectionId = "GlobalDB_SessionRetry_Col1";

            CosmosDatabaseSettings database =
                    client.ReadDatabaseFeedAsync(new FeedOptions())
                        .Result.FirstOrDefault(database1 => database1.Id.Equals(databaseId, StringComparison.OrdinalIgnoreCase));

            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = databaseId });
            }

            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            CosmosContainerSettings collection =
                client.ReadDocumentCollectionFeedAsync(database.SelfLink)
                    .Result.FirstOrDefault(
                        documentCollection => documentCollection.Id.Equals(collectionId, StringComparison.OrdinalIgnoreCase));

            if (collection == null)
            {
                collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings { Id = collectionId });
            }

            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            for (int i = 0; i < 10; i++)
            {
                Document document = await client.CreateDocumentAsync(collection.AltLink, new Document { Id = Guid.NewGuid().ToString() });
                ResourceResponse<Document> response = await client.ReadDocumentAsync(document);
                Document createdDocument = response.Resource;
                Assert.IsNotNull(createdDocument);
            }
        }

        private void ValidateDatabaseResponseBody(CosmosDatabaseSettings database, string databaseName = null)
        {
            Assert.IsNotNull(database.Id, "Id cannot be null");
            Assert.IsNotNull(database.ResourceId, "Resource Id (Rid) cannot be null");
            Assert.IsNotNull(database.SelfLink, "Self link cannot be null");
            Assert.IsNotNull(database.CollectionsLink, "Collections link cannot be null");
            Assert.IsNotNull(database.UsersLink, "Users link cannot be null");
            Assert.IsNotNull(database.ETag, "Etag cannot be null");
            Assert.IsNotNull(database.Timestamp, "Timestamp cannot be null");
            Assert.IsTrue(database.UsersLink.Contains(database.ResourceId), string.Format(CultureInfo.InvariantCulture, "Users link {0} should contain resource id {1}", database.UsersLink, database.ResourceId));
            Assert.IsTrue(database.CollectionsLink.Contains(database.ResourceId), string.Format(CultureInfo.InvariantCulture, "Collections link {0} should contain resource id {1}", database.CollectionsLink, database.ResourceId));
            Assert.IsTrue(database.SelfLink.Contains(database.ResourceId), string.Format(CultureInfo.InvariantCulture, "Self link {0} should contain resource id {1}", database.SelfLink, database.ResourceId));
            if (databaseName != null)
            {
                Assert.AreEqual(databaseName, database.Id, "Id should be match the name");
            }
        }
    }
}
