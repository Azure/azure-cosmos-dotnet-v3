//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
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
            DocumentClient client = TestCommon.CreateClient(false);
            TestCommon.DeleteAllDatabasesAsync().Wait();

            this.writeRegionEndpointUri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            this.masterKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
        }

        [TestMethod]
        public async Task ValidateVectorSessionTokenWithClientSideOptOutAsync()
        {
            bool bEnableMultipleWriteLocations = await TestCommon.IsMultipleWriteLocationsEnabledAsync();

            if (!bEnableMultipleWriteLocations)
            {
                return;
            }

            DocumentClient client = TestCommon.CreateClient(false);

            CosmosContainerSettings documentCollection = TestCommon.CreateOrGetDocumentCollection(client);

            // we must sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            Logger.LogLine("Created collection {0}", documentCollection.Id);

            CosmosAccountSettings databaseAccount = await client.GetDatabaseAccountAsync();

            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];

            CosmosAccountLocation hubLocation = databaseAccount.WriteLocationsInternal[0];
            CosmosAccountLocation satelliteLocation = databaseAccount.WriteLocationsInternal[1];

            ConnectionPolicy policy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Direct,
                ConnectionProtocol = Protocol.Tcp,
                UseMultipleWriteLocations = false,
            };

            policy.PreferredLocations.Add(satelliteLocation.Name);
            using (DocumentClient documentClient = new DocumentClient(new Uri(hubLocation.DatabaseAccountEndpoint), authKey, policy, ConsistencyLevel.Session))
            {
                List<Document> documents = new List<Document>();
                for (int i = 0; i < 10; i++)
                {
                    Document document = await documentClient.CreateDocumentAsync(documentCollection, new Document() { Id = Guid.NewGuid().ToString() });

                    Logger.LogLine("Created non-tentative document {0} with session token {1}", document.Id, documentClient.GetSessionToken(documentCollection.SelfLink));

                    documents.Add(document);
                }

                foreach (Document document in documents)
                {
                    bool documentFound = false;
                    for (int retryCount = 0; retryCount < 10; retryCount++)
                    {
                        try
                        {
                            await documentClient.ReadDocumentAsync(document);

                            documentFound = true;
                            break;
                        }
                        catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            // handle replication latency
                            await Task.Delay(500);
                        }
                    }

                    if (!documentFound)
                    {
                        Assert.Fail("Document with id {0} not found by client with preferred location {1}", document.Id, documentClient.ConnectionPolicy.PreferredLocations[0]);
                    }
                    else
                    {
                        Logger.LogLine("Read non-tentative document {0} by client with preferred location {1} with session token {2}",
                            document.Id, documentClient.ConnectionPolicy.PreferredLocations[0], documentClient.GetSessionToken(documentCollection.SelfLink));
                    }
                }

                // go back to hub region and issue more writes
                for (int i = 0; i < 10; i++)
                {
                    Document document = await documentClient.CreateDocumentAsync(documentCollection, new Document() { Id = Guid.NewGuid().ToString() });

                    Logger.LogLine("Created non-tentative document {0} with session token {1}", document.Id, documentClient.GetSessionToken(documentCollection.SelfLink));
                }
            }
        }



        [TestMethod]
        public async Task ValidateReadBarrierAsync()
        {
            using (new ActivityScope(Guid.NewGuid()))
            {
                DocumentClient client = TestCommon.CreateClient(false);

                CosmosContainerSettings  documentCollection = TestCommon.CreateOrGetDocumentCollection(client);

                // we must sleep to make sure it is replicated.
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                Logger.LogLine("Created collection {0}", documentCollection.Id);


                Task[] tasks = new Task[2];
                tasks[0] = this.CreateUsersAsync(client, 100);
                tasks[1] = this.QueryOffersAsync(documentCollection, 100);

                await Task.WhenAll(tasks);
            }
        }

        private async Task CreateUsersAsync(DocumentClient client, int count)
        {
            CosmosDatabaseSettings database = TestCommon.CreateOrGetDatabase(client);

            for (int i = 0; i < count; i++)
            {
                await client.CreateUserAsync(database, new User() { Id = Guid.NewGuid().ToString() });
            }
        }

        private async Task QueryOffersAsync(CosmosContainerSettings  documentCollection, int count)
        {
            IFabricClient fabricClient = new FabricClientFacade(this.GetType().ToString());

            AdminClient adminClient = TestCommon.CreateAdminClient(fabricClient, "localhost-westus");
            await adminClient.InitializeAsync();

            string systemKeyReadOnly = (await TestCommon.configReader.ReadFederationConfigAsync()).SystemKeyReadOnly;

            for (int i = 0; i < count; i++)
            {
                using (COMMON::Microsoft.Azure.Documents.DocumentServiceRequest request = ResourceHelper.CreateDocumentServiceRequestForQueryingOffer(systemKeyReadOnly, documentCollection.ResourceId))
                {
                    request.Headers[HttpConstants.HttpHeaders.ConsistencyLevel] = ConsistencyLevel.BoundedStaleness.ToString();
                    await adminClient.StoreClient.ProcessMessageAsync(request);
                }
            }
        }

        [TestMethod]
        public async Task ValidateVectorSessionTokenAsync()
        {
            await this.ValidateVectorSessionTokenAsync(ConnectionMode.Direct);
            await this.ValidateVectorSessionTokenAsync(ConnectionMode.Gateway);
        }

        public async Task ValidateVectorSessionTokenAsync(ConnectionMode connectionMode)
        {
            bool bEnableMultipleWriteLocations = await TestCommon.IsMultipleWriteLocationsEnabledAsync();

            if (!bEnableMultipleWriteLocations)
            {
                return;
            }

            DocumentClient client = TestCommon.CreateClient(false);

            CosmosContainerSettings  documentCollection = TestCommon.CreateOrGetDocumentCollection(client);

            // we must sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            Logger.LogLine("Created collection {0}", documentCollection.Id);

            CosmosAccountSettings databaseAccount = await client.GetDatabaseAccountAsync();

            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            List<DocumentClient> documentClients = new List<DocumentClient>();
            foreach (CosmosAccountLocation writeLocation in databaseAccount.WriteLocationsInternal)
            {
                ConnectionPolicy policy = new ConnectionPolicy
                {
                    ConnectionMode = connectionMode,
                    ConnectionProtocol = Protocol.Tcp,
                    UseMultipleWriteLocations = true,
                };

                policy.PreferredLocations.Add(writeLocation.Name);
                DocumentClient documentClient = new DocumentClient(new Uri(writeLocation.DatabaseAccountEndpoint), authKey, policy, ConsistencyLevel.Session);
                documentClients.Add(documentClient);

                Logger.LogLine("CosmosAccountSettings has write location {0} with endpoint {1}", writeLocation.Name, writeLocation.DatabaseAccountEndpoint);
            }

            try
            {
                for (int i = 0; i < 3; i++)
                {
                    await this.ValidateVectorSessionTokenAsync(new ReadOnlyCollection<DocumentClient>(documentClients), databaseAccount, documentCollection);
                }
            }
            finally
            {
                foreach (DocumentClient documentClient in documentClients)
                {
                    documentClient.Dispose();
                }
            }
        }

        private async Task ValidateVectorSessionTokenAsync(ReadOnlyCollection<DocumentClient> documentClients, CosmosAccountSettings databaseAccount, CosmosContainerSettings  documentCollection)
        {
            // Do writes in hub region
            List<Document> documents = new List<Document>();
            for (int i = 0; i < 10; i++)
            {
                Document document = await documentClients[0].CreateDocumentAsync(documentCollection, new Document() { Id = Guid.NewGuid().ToString() });

                Logger.LogLine("Created non-tentative document {0} with session token {1}", document.Id, documentClients[0].GetSessionToken(documentCollection.SelfLink));

                documents.Add(document);
            }

            // Do reads from each region
            foreach (DocumentClient documentClient in documentClients)
            {
                foreach (Document document in documents)
                {
                    bool documentFound = false;
                    for (int retryCount = 0; retryCount < 10; retryCount++)
                    {
                        try
                        {
                            await documentClient.ReadDocumentAsync(document);

                            documentFound = true;
                            break;
                        }
                        catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            // handle replication latency
                            await Task.Delay(500);
                        }
                    }

                    if (!documentFound)
                    {
                        Assert.Fail("Document with id {0} not found by client with preferred location {1}", document.Id, documentClient.ConnectionPolicy.PreferredLocations[0]);
                    }
                    else
                    {
                        Logger.LogLine("Read non-tentative document {0} by client with preferred location {1} with session token {2}",
                            document.Id, documentClient.ConnectionPolicy.PreferredLocations[0], documentClient.GetSessionToken(documentCollection.SelfLink));
                    }
                }
            }

            // Now do cross region reads
            foreach (DocumentClient documentClient in documentClients)
            {
                // cache the previous preferred location
                string previousPreferredLocation = documentClient.ConnectionPolicy.PreferredLocations[0];

                foreach (CosmosAccountLocation location in databaseAccount.WriteLocationsInternal)
                {
                    documentClient.ConnectionPolicy.PreferredLocations.Clear();
                    documentClient.ConnectionPolicy.PreferredLocations.Add(location.Name);

                    foreach (Document document in documents)
                    {
                        Logger.LogLine("Reading document {0} by client with previous preferred location = {1}, current preferred location {2} with session token {3}",
                            document.Id, previousPreferredLocation, location.Name, documentClient.GetSessionToken(documentCollection.SelfLink));

                        await documentClient.ReadDocumentAsync(document);
                    }
                }

                // restore preferred locations to previous value
                documentClient.ConnectionPolicy.PreferredLocations.Clear();
                documentClient.ConnectionPolicy.PreferredLocations.Add(previousPreferredLocation);
            }

            // Now do some tentative writes
            for (int i = 0; i < 10; i++)
            {
                Document document = await documentClients[1].CreateDocumentAsync(documentCollection, new Document() { Id = Guid.NewGuid().ToString() });

                Logger.LogLine("Created tentative document {0} with session token {1}", document.Id, documentClients[1].GetSessionToken(documentCollection.SelfLink));

                documents.Add(document);
            }

            // Do reads from each region
            foreach (DocumentClient documentClient in documentClients)
            {
                foreach (Document document in documents)
                {
                    Logger.LogLine("Reading document {0} by client with preferred location = {1} with session token {2}",
                        document.Id, documentClient.ConnectionPolicy.PreferredLocations[0], documentClient.GetSessionToken(documentCollection.SelfLink));

                    bool documentFound = false;
                    for (int retryCount = 0; retryCount < 10; retryCount++)
                    {
                        try
                        {
                            await documentClient.ReadDocumentAsync(document);

                            documentFound = true;
                            break;
                        }
                        catch (DocumentClientException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                        {
                            // handle AE replication latency
                            await Task.Delay(500);
                        }
                    }

                    if (!documentFound)
                    {
                        Assert.Fail("Document with id {0} not found by client with preferred location {1}", document.Id, documentClient.ConnectionPolicy.PreferredLocations[0]);
                    }
                    else
                    {
                        Logger.LogLine("Read document {0} by client with preferred location {1} with session token {2}",
                            document.Id, documentClient.ConnectionPolicy.PreferredLocations[0], documentClient.GetSessionToken(documentCollection.SelfLink));
                    }
                }
            }

            // Again do cross region reads and ensure that each client can read all documents from each region
            foreach (DocumentClient documentClient in documentClients)
            {
                // cache the previous preferred location
                string previousPreferredLocation = documentClient.ConnectionPolicy.PreferredLocations[0];

                foreach (CosmosAccountLocation location in databaseAccount.WriteLocationsInternal)
                {
                    documentClient.ConnectionPolicy.PreferredLocations.Clear();
                    documentClient.ConnectionPolicy.PreferredLocations.Add(location.Name);

                    foreach (Document document in documents)
                    {
                        Logger.LogLine("Reading document {0} by client with previous preferred location = {1}, current preferred location {2}, session token {3}",
                            document.Id, previousPreferredLocation, location.Name, documentClient.GetSessionToken(documentCollection.SelfLink));

                        await documentClient.ReadDocumentAsync(document);
                    }
                }

                // restore preferred locations to previous value
                documentClient.ConnectionPolicy.PreferredLocations.Clear();
                documentClient.ConnectionPolicy.PreferredLocations.Add(previousPreferredLocation);
            }
        }

        [TestMethod]
        public async Task ValidateOfferReplace()
        {
            DocumentClient client = TestCommon.CreateClient(false);

            CosmosDatabaseSettings database = (await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString("N") })).Resource;

            CosmosContainerSettings  collection =
                await client.CreateDocumentCollectionAsync(
                    database.SelfLink,
                    new CosmosContainerSettings  { Id = Guid.NewGuid().ToString("N") },
                    new RequestOptions { OfferType = "S3" });

            Offer offer = (await client.ReadOffersFeedAsync()).Single(o => o.ResourceLink == collection.SelfLink);

            Assert.AreEqual(offer.OfferType, "S3");
            Assert.AreEqual(offer.OfferVersion, Constants.Offers.OfferVersion_V1);

            AdminClient adminClient = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()));
            await adminClient.InitializeAsync();

            PartitionResource writeRegionCollectionPartition;
            using (new ActivityScope(Guid.NewGuid()))
            {
                COMMONRM::Topology topology = await adminClient.ReadTopologyResourceAsync(adminClient.MasterServiceIdentity);
                IList<PartitionResource> readPartitionResources = (await adminClient.ListPartitionResourcesAsync(adminClient.MasterServiceIdentity, topology.WriteRegion)).ToList();
                writeRegionCollectionPartition = readPartitionResources.Single(partition =>
                    partition.ServiceType == FabricServiceType.ServerService.ToString() &&
                    partition.CollectionOrDatabaseResourceId == collection.ResourceId);
            }

            Offer targetOffer = new Offer(offer) { OfferType = "S2" };
            await client.ReplaceOfferAsync(targetOffer);

            string federationId;
            string serviceName;
            ReplicatorAddressHelper.TryParseWellKnownServiceUri(true, new Uri(writeRegionCollectionPartition.WellKnownServiceUrl), out federationId, out serviceName);

            using (new ActivityScope(Guid.NewGuid()))
            {
                COMMON::Microsoft.Azure.Documents.Offer readOffer = (await adminClient.ListOfferResourcesAsync(new COMMON::Microsoft.Azure.Documents.ServiceIdentity(federationId, new Uri(serviceName), false))).Single();
                Assert.AreEqual(targetOffer.OfferType, readOffer.OfferType);
            }

            OfferV2 targetOffer2 = new OfferV2(targetOffer, 5000);
            await client.ReplaceOfferAsync(targetOffer2);
            using (new ActivityScope(Guid.NewGuid()))
            {
                COMMON::Microsoft.Azure.Documents.Offer readOffer = (await adminClient.ListOfferResourcesAsync(new COMMON::Microsoft.Azure.Documents.ServiceIdentity(federationId, new Uri(serviceName), false))).Single();
                Assert.AreEqual("Invalid", readOffer.OfferType);

                Assert.AreEqual(targetOffer2.Content.OfferThroughput, ((COMMON::Microsoft.Azure.Documents.OfferV2)readOffer).Content.OfferThroughput);
            }

            targetOffer2 = new OfferV2(targetOffer2, 10000);
            await client.ReplaceOfferAsync(targetOffer2);
            using (new ActivityScope(Guid.NewGuid()))
            {
                COMMON::Microsoft.Azure.Documents.Offer readOffer = (await adminClient.ListOfferResourcesAsync(new COMMON::Microsoft.Azure.Documents.ServiceIdentity(federationId, new Uri(serviceName), false))).Single();
                Assert.AreEqual("Invalid", readOffer.OfferType);

                Assert.AreEqual(targetOffer2.Content.OfferThroughput, ((COMMON::Microsoft.Azure.Documents.OfferV2)readOffer).Content.OfferThroughput);
            }
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestClientWithNoFailover()
        {
            TestClientWithNoFailoverAsync().Wait();
        }

        private async Task TestClientWithNoFailoverAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Uri readRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint2"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            // #1. No failover on client side, verify write on readRegion failed.
            ConnectionPolicy noFailoverPolicy = new ConnectionPolicy() { EnableEndpointDiscovery = false };

            DocumentClient client1 = new DocumentClient(
                writeRegionUri,
                authKey,
                noFailoverPolicy);

            CosmosDatabaseSettings database;
            CosmosContainerSettings  collection = TestCommon.CreateOrGetDocumentCollection(client1, out database);

            // we must sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            DocumentClient client2 = new DocumentClient(
                readRegionUri,
                authKey,
                noFailoverPolicy);
            try
            {
                await client2.CreateDocumentAsync(collection.AltLink, new Document { Id = Guid.NewGuid().ToString() });
                Assert.Fail("Expected exception when writing to read region using name link");
            }
            catch (DocumentClientException clientException)
            {
                // Bug: If we don't sleep GlobalDatabaseAccountTests.WaitDurationForAsyncReplication, it will return 
                // HttpStatusCode.ServiceUnavailable Currently backend return E_INVALID_COLLECTION_OR_RANGE at StoreProvider::ValidateReferentialIntegrity at StoreProvider.cpp:693
                // TestCommon.AssertException(clientException, HttpStatusCode.ServiceUnavailable);
                TestCommon.AssertException(clientException, HttpStatusCode.Forbidden);
            }

            try
            {
                await client2.CreateDocumentAsync(collection.SelfLink, new Document { Id = Guid.NewGuid().ToString() });
                Assert.Fail("Expected exception when writing to read region using self link");
            }
            catch (DocumentClientException clientException)
            {
                // Bug: If we don't sleep GlobalDatabaseAccountTests.WaitDurationForAsyncReplication, it will return 
                // TestCommon.AssertException(clientException, HttpStatusCode.NotFound);
                TestCommon.AssertException(clientException, HttpStatusCode.Forbidden);
            }

        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestClientWithPreferredRegion()
        {
            TestClientWithPreferredRegionAsync().Wait();
        }

        private async Task TestClientWithPreferredRegionAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Uri readRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint2"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            // #1. Enable failover on client side, verify write succeed on readRegion.
            ConnectionPolicy failoverPolicy = new ConnectionPolicy();
            failoverPolicy.PreferredLocations.Add("West US");

            DocumentClient client3 = new DocumentClient(
                readRegionUri,
                authKey,
                failoverPolicy,
                ConsistencyLevel.Eventual);

            // write should succeed as it will automatic endpoint discovery
            CosmosDatabaseSettings database = await client3.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() });
            CosmosContainerSettings  collection = await client3.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings  { Id = Guid.NewGuid().ToString() });

            // make sure it is replicated
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            CosmosContainerSettings  collection1 = await client3.ReadDocumentCollectionAsync(collection.SelfLink);
            Document document1 = await client3.CreateDocumentAsync(collection.AltLink, new Document { Id = Guid.NewGuid().ToString() });

            // #2. Add the preferred read region. Read should go to read region.
            // make sure it is replicated
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);
            ResourceResponse<Document> response1 = await client3.ReadDocumentAsync(document1);
            Uri uri = new Uri(response1.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(1045, uri.Port, "Read should go to port 1045");

            failoverPolicy.PreferredLocations.Clear();
            failoverPolicy.PreferredLocations.Add("South Central US");

            ResourceResponse<Document> response2 = await client3.ReadDocumentAsync(document1);
            Uri uri2 = new Uri(response2.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(443, uri2.Port, "Read should go to port 443");

            // #3. No preferred read region. Read should go to the write region.
            failoverPolicy.PreferredLocations.Clear();

            ResourceResponse<Document> response3 = await client3.ReadDocumentAsync(document1);
            Uri uri3 = new Uri(response3.ResponseHeaders[HttpConstants.HttpHeaders.ContentLocation]);
            Assert.AreEqual(443, uri3.Port, "Read should go to port 443");

        }

        [TestMethod]
        public void TestUpsertOperationWithPreferredRegion()
        {
            TestUpsertOperationWithPreferredRegionAsync().Wait();
        }

        private async Task TestUpsertOperationWithPreferredRegionAsync()
        {
            Uri globalEndpointUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            ConnectionPolicy connectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway };
            connectionPolicy.PreferredLocations.Add("West US");

            DocumentClient client = new DocumentClient(globalEndpointUri, authKey, connectionPolicy);

            CosmosDatabaseSettings database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() });
            CosmosContainerSettings  collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings  { Id = Guid.NewGuid().ToString() });

            // todo: SessionToken container has a bug which prevent the session consistency read. So we sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            try
            {
                Document document =
                    await client.UpsertDocumentAsync(collection.SelfLink, new Document { Id = Guid.NewGuid().ToString() });
            }
            catch (DocumentClientException ex)
            {
                Assert.Fail(ex.Message);
            }

            connectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct };
            connectionPolicy.PreferredLocations.Add("West US");

            client = new DocumentClient(globalEndpointUri, authKey, connectionPolicy);

            try
            {
                Document document =
                    await client.UpsertDocumentAsync(collection.SelfLink, new Document { Id = Guid.NewGuid().ToString() });
            }
            catch (DocumentClientException ex)
            {
                Assert.Fail(ex.Message);
            }
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
            CosmosContainerSettings  collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings  { Id = Guid.NewGuid().ToString() });

            // todo: SessionToken container has a bug which prevent the session consistency read. So we sleep to make sure it is replicated.
            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            Document document =
                await client.CreateDocumentAsync(collection.SelfLink, new Document { Id = Guid.NewGuid().ToString() });

            Assert.AreEqual(client.WriteEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);
            // Ensure that the ReadEndpoint gets set to whatever is the first region in PreferredLocations irrespective whether it's read or write region
            Assert.AreEqual(client.ReadEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
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
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
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
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestDocumentClientMemoryLeakGatewayHttps()
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                ConnectionProtocol = Protocol.Https
            };


            this.TestDocumentClientMemoryLeakPrivate(connectionPolicy);
        }


        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", Justification = "This is a test for checking memory leak fix which requires me to run GC.Collect")]
        private void TestDocumentClientMemoryLeakPrivate(ConnectionPolicy connectionPolicy)
        {
            Uri globalEndpointUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            DocumentClient client = new DocumentClient(globalEndpointUri, authKey, connectionPolicy);

            // Holding a WeakReference to client to test whether it gets garbage collected eventually
            WeakReference reference = new WeakReference(client, true);

            // Executing any request using this client
            client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() }).Wait();

            // Verify that the Write and Read Endpoints point to same endpoint(since no PreferredLocations was specified)
            Assert.AreEqual(client.WriteEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Assert.AreEqual(client.ReadEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);

            // Adding a preferred read location, which should trigger the event handler to update the Read and Write endpoints
            connectionPolicy.PreferredLocations.Add(ConfigurationManager.AppSettings["Location2"]);

            client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = Guid.NewGuid().ToString() }).Wait();

            // Verify that the read endpoint now changes to this new preferred location
            Assert.AreEqual(client.WriteEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);
            Assert.AreEqual(client.ReadEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint2"]);

            // Disposing the client and setting it to null to enable garbage collection
            client.Dispose();
            client = null;

            // Forcing the GC to run and garbage collect client instance memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Verify that the client instance(target for this reference) is not alive any more
            Assert.IsTrue(!reference.IsAlive, "Memory leak");
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestDatabaseAccountRegionList()
        {
            TestDatabaseAccountRegionListAsync().Wait();
        }

        private async Task TestDatabaseAccountRegionListAsync()
        {
            using (DocumentClient client = TestCommon.CreateClient(true))
            {
                CosmosAccountSettings account = await client.GetDatabaseAccountAsync();

                Assert.AreEqual(account.WriteLocationsInternal.Count, account.EnableMultipleWriteLocations ? 2 : 1);
                Assert.AreEqual(account.WriteLocationsInternal[0].DatabaseAccountEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint"]);
                Assert.AreEqual(account.WriteLocationsInternal[0].Name, ConfigurationManager.AppSettings["Location"]);

                Assert.AreEqual(account.ReadLocationsInternal.Count, 2);
                Assert.AreEqual(account.ReadLocationsInternal[1].DatabaseAccountEndpoint, ConfigurationManager.AppSettings["GatewayEndpoint2"]);
                Assert.AreEqual(account.ReadLocationsInternal[1].Name, "West US");
            }

            // Verify all local and global db account naming service is populated
            COMMONNAMESPACE.IFabricClient fabricClient;
            fabricClient = new FabricClientFacade(this.GetType().ToString());
            DocumentServiceConfiguration config = await new CompositeConfigurationReader(fabricClient).ReadDocumentServiceConfigAsync(GlobalDatabaseAccountName);
            Assert.IsTrue(config.IsDatabaseAccountGeoEnabled, "Global geo-enabled account");
            DocumentServiceConfiguration config1 = await new CompositeConfigurationReader(fabricClient).ReadDocumentServiceConfigAsync("localhost");
            Assert.IsTrue(config1.IsDatabaseAccountGeoEnabled, "Localhost geo-enabled account");
            DocumentServiceConfiguration config2 = await new CompositeConfigurationReader(fabricClient).ReadDocumentServiceConfigAsync("localhost-westus");
            Assert.IsTrue(config2.IsDatabaseAccountGeoEnabled, "Localhost-westus geo-enabled account");

        }

        [TestMethod]
        public void TestMasterCRUD()
        {
            ValidateMasterCRUDAsync().Wait();
        }

        [TestMethod]
        public void ValidatePartitionResourceCRUD()
        {
            this.ValidatePartitionResourceCRUDAsync().Wait();
        }

        private async Task ValidatePartitionResourceCRUDAsync()
        {
            AdminClient adminClient = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()));
            await adminClient.InitializeAsync();

            using (new ActivityScope(Guid.NewGuid()))
            {
                const string region = "Canada";
                List<PartitionResource> createdPartitionResources = new List<PartitionResource>();
                for (int index = 0; index < 20; index++)
                {
                    PartitionResource resource = await adminClient.CreatePartitionResourceAsync(
                        new PartitionResource()
                        {
                            CollectionOrDatabaseResourceId = "7bgNMIosC24=",
                            ServiceType = FabricServiceType.ServerService.ToString(),
                            WellKnownServiceUrl = string.Format(CultureInfo.InvariantCulture, "fabric:/app/svc{0}", index),
                            LinkRelationType = (int)LinkRelationTypes.Geo,
                            Region = region
                        },
                        adminClient.MasterServiceIdentity);

                    createdPartitionResources.Add(resource);
                }

                IList<PartitionResource> readPartitionResources = (await adminClient.ListPartitionResourcesAsync(adminClient.MasterServiceIdentity, region)).ToList();
                foreach (PartitionResource createdPartitionResource in createdPartitionResources)
                {
                    Assert.IsTrue(
                        readPartitionResources.Any(
                        readResource =>
                            readResource.CollectionOrDatabaseResourceId == createdPartitionResource.CollectionOrDatabaseResourceId &&
                            readResource.ServiceType == createdPartitionResource.ServiceType &&
                            readResource.WellKnownServiceUrl == createdPartitionResource.WellKnownServiceUrl &&
                            readResource.LinkRelationType == createdPartitionResource.LinkRelationType &&
                            readResource.Region == region));
                }

                foreach (PartitionResource readPartitionResource in readPartitionResources)
                {
                    await adminClient.DeletePartitionResourceIfExistsAsync(readPartitionResource.ResourceId, adminClient.MasterServiceIdentity);
                }

                readPartitionResources = (await adminClient.ListPartitionResourcesAsync(adminClient.MasterServiceIdentity, region)).ToList();
                Assert.AreEqual(0, readPartitionResources.Count);
            }
        }

        [TestMethod]
        public void TestTopologyWriteStatus()
        {
            this.ValidateWriteStatus().Wait();
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined in gated runs */]
        public void TestFailoverAPIs()
        {
            this.ValidateFailoverAPIs().Wait();
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestGeoCollectionCRUD()
        {
            ValidateCollectionCRUDAsync().Wait();
        }

        [TestMethod]
        [TestCategory("Quarantine") /* Used to filter out quarantined tests in gated runs */]
        public void TestGeoPartitionedCollectionCRUD()
        {
            ValidatePartitionedCollectionCRUDAsync().Wait();
        }

        [TestMethod]
        public void TestFailoverWriteOperationRetryPolicy()
        {
            this.TestFailoverWriteOperationRetryPolicyAsync().Wait();
        }

        [TestMethod]
        public void ValidateUpdateServiceManagerConfigOperation()
        {
            this.ValidateUpdateServiceManagerConfigOperationAsync().Wait();
        }

        [TestMethod]
        public void ValidateCrossRegionCapacityAllocationWorkflow()
        {
            this.ValidateCrossRegionCapacityAllocationWorkflowAsync().Wait();
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

        private async Task TestFailoverWriteOperationRetryPolicyAsync()
        {
            OperationTrace tracingOperation = new OperationTrace(Guid.NewGuid().ToString());

            AdminClient adminClient = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()));
            await adminClient.InitializeAsync();

            using (new ActivityScope(Guid.NewGuid()))
            {
                // save the original topology
                COMMON::Microsoft.Azure.Documents.Topology originalTopology
                    = await adminClient.ReadTopologyResourceAsync(adminClient.MasterServiceIdentity);
                originalTopology.ResourceId = null;

                originalTopology.SetMajorIncrementGlobalConfigNumber(originalTopology.GlobalConfigurationNumber);

                Func<bool, Task<COMMON::Microsoft.Azure.Documents.Topology>> delegate1 = async (bool isEtagMismatch) =>
                {
                    if (!isEtagMismatch)
                    {
                        return await adminClient.GrantWriteStatusAsync(originalTopology, adminClient.MasterServiceIdentity, originalTopology.ETag);
                    }
                    else
                    {
                        Assert.Fail("it should never come here!");
                        throw new Exception("It should never come here!");
                    }
                };

                COMMON::Microsoft.Azure.Documents.Topology topology1 = await COMMON::Microsoft.Azure.Documents.BackoffRetryUtility<COMMON::Microsoft.Azure.Documents.Topology>.ExecuteAsync(
                    delegate1,
                    new FailoverWorkflowOperation.FailoverWriteOperationRetryPolicy(tracingOperation),
                    default(CancellationToken));

                Assert.AreNotEqual(topology1.ETag, originalTopology.ETag);

                originalTopology.SetMajorIncrementGlobalConfigNumber(originalTopology.GlobalConfigurationNumber);

                int countofEtagMismatch = 0;
                Func<bool, Task<COMMON::Microsoft.Azure.Documents.Topology>> delegate2 = async (bool isEtagMismatch) =>
                {
                    countofEtagMismatch++;
                    if (!isEtagMismatch)
                    {
                        Assert.IsTrue(countofEtagMismatch == 1);
                        // second time, ETAG changed, this should trigger conflict.
                        return await adminClient.GrantWriteStatusAsync(originalTopology, adminClient.MasterServiceIdentity, originalTopology.ETag);
                    }
                    else
                    {
                        Assert.IsTrue(countofEtagMismatch == 2);
                        return await adminClient.GrantWriteStatusAsync(originalTopology, adminClient.MasterServiceIdentity, topology1.ETag);
                    }
                };

                // do it second time, it should fail first because ETAG mismatch
                COMMON::Microsoft.Azure.Documents.Topology topology2 = await COMMON::Microsoft.Azure.Documents.BackoffRetryUtility<COMMON::Microsoft.Azure.Documents.Topology>.ExecuteAsync(
                    delegate2,
                    new FailoverWorkflowOperation.FailoverWriteOperationRetryPolicy(tracingOperation),
                    default(CancellationToken));

                Assert.AreNotEqual(topology1.ETag, topology2.ETag);

            }
        }

        private async Task ValidateGetDatabaseAccountFromGatewayAsync()
        {
            Uri writeRegionUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            string authKey = ConfigurationManager.AppSettings["MasterKey"];

            using (DocumentClient client = new DocumentClient(writeRegionUri, authKey))
            {
                CosmosAccountSettings databaseAccount = await client.GetDatabaseAccountAsync();
                Assert.AreEqual(databaseAccount.EnableMultipleWriteLocations ? 2 : 1,
                    databaseAccount.WriteLocationsInternal.Count);
                Assert.AreEqual(2, databaseAccount.ReadLocationsInternal.Count);
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
                        .Result.FirstOrDefault(database1 => database1.Id.Equals(databaseId, StringComparison.InvariantCultureIgnoreCase));

            if (database == null)
            {
                database = await client.CreateDatabaseAsync(new CosmosDatabaseSettings { Id = databaseId });
            }

            await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            CosmosContainerSettings  collection =
                client.ReadDocumentCollectionFeedAsync(database.SelfLink)
                    .Result.FirstOrDefault(
                        documentCollection => documentCollection.Id.Equals(collectionId, StringComparison.InvariantCultureIgnoreCase));

            if (collection == null)
            {
                collection = await client.CreateDocumentCollectionAsync(database.SelfLink, new CosmosContainerSettings  { Id = collectionId });
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

            bool wasTriggered = false;
            mockStoreModel.Setup(model => model.ProcessMessageAsync(It.IsAny<DocumentServiceRequest>(), default(CancellationToken)))
                .Returns<DocumentServiceRequest, CancellationToken>(async (r, cancellationToken) =>
                {
                    if (wasTriggered)
                    {
                        return await this.CreateEmptyDocumentServiceResponse();
                    }
                    wasTriggered = true;
                    throw this.CreateReadSessionNotAvailableException();
                });

            client.StoreModel = mockStoreModel.Object;
            client.GatewayStoreModel = mockStoreModel.Object;

            ResourceResponse<Database> dbResponse = await client.ReadDatabaseAsync("/dbs/id1");
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
                ResourceFeedReader<Database> dbFeed = client.CreateDatabaseFeedReader();
                FeedResponse<Database> response = await dbFeed.ExecuteNextAsync();
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

        private Task<DocumentServiceResponse> CreateEmptyDocumentServiceResponse()
        {
            var tcs = new TaskCompletionSource<DocumentServiceResponse>();
            Task.Run(
                () =>
                {
                    Thread.Sleep(100);
                    tcs.SetResult(new DocumentServiceResponse(new MemoryStream(), new StringKeyValueCollection(), HttpStatusCode.OK));
                });

            return tcs.Task;
        }

        private DocumentClientException CreateReadSessionNotAvailableException()
        {
            HttpResponseMessage responseMessage = new HttpResponseMessage();
            responseMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            responseMessage.Headers.Add(WFConstants.BackendHeaders.SubStatus, 1002.ToString(CultureInfo.InvariantCulture));

            Error error = new Error() { Code = "404", Message = "Message: {'Errors':['The read session is not available for the input session token.']}" };

            return new DocumentClientException(error, responseMessage.Headers, (HttpStatusCode)404);
        }

        private async Task ValidateCrossRegionCapacityAllocationWorkflowAsync()
        {
            TestServiceProvider serviceProviderForEmulator = new TestServiceProvider(new TestAdminClientFactory(), "v1");
            IStoreProvider storeProviderForEmulator = serviceProviderForEmulator.GetService<IStoreProvider>();

            List<string> federationNames = new List<string>()
            {
                "federation1",
                "federation2"
            };

            List<string> federationLocations = new List<string>()
            {
                "region1",
                "region2"
            };

            FederationEntity entityTemplate = storeProviderForEmulator.ListAsync<FederationEntity>(null).Result[0];
            IList<DocumentServiceManagerStateEntity> dsmEntities = await storeProviderForEmulator.ListAsync<DocumentServiceManagerStateEntity>("emulatorfederation");

            IFabricClient fabricClientFacade = new FabricClientFacade(new FabricClient(), this.GetType().ToString());
            IConfigurationReader configurationReader =
                new CompositeConfigurationReader(
                    fabricClientFacade);

            NamingServiceConfigurationWriter namingServiceConfigurationWriter =
                new NamingServiceConfigurationWriter(
                    fabricClientFacade);

            TestServiceProvider serviceProvider = new TestServiceProvider(new TestAdminClientFactory());
            ManagementUtil.Initialize(serviceProvider).Wait();
            IStoreProvider storeProvider = serviceProvider.GetService<IStoreProvider>();

            const int numFederations = 2;
            const int numApplicationsPerFederation = 2;
            const int crossRegionAllocationCount = 12;
            for (int federationIndex = 0; federationIndex < numFederations; federationIndex++)
            {
                string federationName = federationNames[federationIndex];

                entityTemplate.Id = federationName;
                entityTemplate.Location = federationLocations[federationIndex];

                await storeProvider.CreateAsync<FederationEntity>(entityTemplate);

                foreach (DocumentServiceManagerStateEntity dsmEntityTemplate in dsmEntities)
                {
                    dsmEntityTemplate.FederationId = federationName;
                    await storeProvider.CreateAsync<DocumentServiceManagerStateEntity>(dsmEntityTemplate);
                }

                // create applications for federation
                for (int applicationIndex = 0; applicationIndex < numApplicationsPerFederation; applicationIndex++)
                {
                    ServiceManagerConfiguration config =
                        new ServiceManagerConfiguration();
                    string randomAppSuffix = Guid.NewGuid().ToString();
                    string appName = @"fabric:/" + federationName + applicationIndex + "/" + randomAppSuffix;

                    string nodeGroupName = randomAppSuffix;

                    Uri configUri =
                        ServicesPoolManagementHelper.GetServicePoolManagerConfigUri(true, federationName, new Uri(appName), nodeGroupName, FabricServiceType.ServerService);

                    Logger.LogLine("ServiceConfigUri : {0}", configUri.AbsoluteUri);

                    Shared.BackoffRetryUtility<bool>.ExecuteAsync(async () =>
                    {
                        try
                        {
                            await fabricClientFacade.CreateNameAsync(configUri);
                        }
                        catch (FabricElementAlreadyExistsException)
                        {
                            Logger.LogLine("Name {0} already exists", configUri.ToString());
                        }
                        catch (Exception e) when (e.InnerException != null && e.InnerException is FabricElementAlreadyExistsException)
                        {
                            Logger.LogLine("Name {0} already exists", configUri.ToString());
                        }

                        return true;
                    }, new FabricExponentialRetryPolicy()).Wait();

                    List<ServicePoolLimits> servicePoolLimits =
                        new List<ServicePoolLimits>();
                    for (int servicePoolIndex = 0; servicePoolIndex < 2; servicePoolIndex++)
                    {
                        ServicePool servicePool = new ServicePool(true, federationName,
                            FabricServiceType.ServerService.ToString() + "/" + servicePoolIndex, fabricClientFacade, true);
                        servicePool.CreateAsync(default(CancellationToken)).Wait();

                        servicePoolLimits.Add(new ServicePoolLimits()
                        {
                            PoolId = servicePoolIndex,
                            FreePercentageToAllocate = 100,
                            Capacity = 10
                        });

                        for (int serviceIndex = 0; serviceIndex < 10; serviceIndex++)
                        {
                            Uri serviceUri = new Uri(appName + "/svc" + servicePoolIndex.ToString() + "-" + serviceIndex.ToString());

                            Shared.BackoffRetryUtility<bool>.ExecuteAsync(async () =>
                            {
                                try
                                {
                                    await fabricClientFacade.CreateNameAsync(serviceUri);
                                }
                                catch (FabricElementAlreadyExistsException)
                                {
                                    Logger.LogLine("Name {0} already exists", serviceUri.ToString());
                                }
                                catch (Exception e) when (e.InnerException != null && e.InnerException is FabricElementAlreadyExistsException)
                                {
                                    Logger.LogLine("Name {0} already exists", serviceUri.ToString());
                                }

                                return true;
                            }, new FabricExponentialRetryPolicy()).Wait();

                            FabricServiceConfiguration serviceConfig = new FabricServiceConfiguration()
                            {
                                DatabaseAccountName = WindowsFabricConstants.DefaultAccountNamePrefix + "0",
                                ServicePoolIndex = servicePoolIndex,
                                ServiceState = ServiceState.Bindable
                            };

                            await namingServiceConfigurationWriter.UpdatePropertyAsync(serviceUri, serviceConfig);
                            await servicePool.AddServiceToBindablePoolAsync(serviceUri, default(CancellationToken));
                        }
                    }

                    config.Region = "region1" + (federationIndex + 1);
                    config.ApplicationName = new Uri(appName);
                    config.NodeGroupName = nodeGroupName;
                    config.PoolLimits = servicePoolLimits;
                    config.DefaultAccountsCount = 10;
                    config.TotalPartitionCount = 2 * 2;
                    config.FederationId = federationName;
                    config.FederationIndex = 1;
                    config.FabricServiceDescription = JsonConvert.SerializeObject(new COMMON::Microsoft.Azure.Documents.Common.Fabric.FabricService());

                    if (federationIndex == 1)
                    {
                        List<CrossRegionCapacityReservationPolicy> policy = new List<CrossRegionCapacityReservationPolicy>();
                        config.CrossRegionCapacityReservationPolicies = policy;
                    }

                    await namingServiceConfigurationWriter.UpdatePropertyAsync(configUri, config);
                }
            }

            Management.CrossRegionCapacityAllocationResource resource = new Management.CrossRegionCapacityAllocationResource();
            resource.CapacityAllocation.Add(
                new Management.FederationCapacityAllocation()
                {
                    Region = "region1",
                    ServerServiceAllocationCount = crossRegionAllocationCount,
                    FreePercentageToAllocate = 100,
                    TargetFederationId = federationNames[0],
                });

            WorkflowOperation<bool> crossRegionCapacityAllocationWorkflow = new CrossRegionCapacityAllocationWorkflow(
                federationLocations[1],
                federationNames[1],
                resource);
            crossRegionCapacityAllocationWorkflow.SubscriptionId = Guid.NewGuid().ToString();
            ManagementUtil.WaitForOperation(crossRegionCapacityAllocationWorkflow);

            IList<ServiceManagerConfiguration> serviceManagerConfigs = await configurationReader.ReadFabricServiceManagerConfigurationsAsync(FabricServiceType.ServerService.ToString(), default(CancellationToken), true,
                federationNames[1]);

            foreach (ServiceManagerConfiguration serviceManagerConfig in serviceManagerConfigs)
            {
                foreach (CrossRegionCapacityReservationPolicy policy in serviceManagerConfig.CrossRegionCapacityReservationPolicies)
                {
                    Assert.AreEqual(crossRegionAllocationCount / numApplicationsPerFederation, policy.ServiceCount);
                }
            }

            // negative test
            WorkflowOperation<bool> crossRegionCapacityAllocationWorkflowFailed = new CrossRegionCapacityAllocationWorkflow(
                "region1",
                federationNames[0],
                resource);
            crossRegionCapacityAllocationWorkflowFailed.SubscriptionId = Guid.NewGuid().ToString();
            ManagementUtil.WaitForOperation(crossRegionCapacityAllocationWorkflowFailed, OperationStatus.Failed);
        }

        private async Task ValidateUpdateServiceManagerConfigOperationAsync()
        {
            TestFabricClientEndpointResolver testFabricClientEndpointResolver = new TestFabricClientEndpointResolver();
            string[] endpoints = { EmulatorConstants.Fabric.ConnectionEndpoint };
            IFabricClient fabricClientFacade = new FabricClientFacade(new FabricClient(), this.GetType().ToString());
            testFabricClientEndpointResolver.SetFabricClient(endpoints, fabricClientFacade);
            TestServiceProvider serviceProvider = new TestServiceProvider(new TestAdminClientFactory(), testFabricClientEndpointResolver, "v1");
            await ManagementUtil.Initialize(serviceProvider);
            IStoreProvider store = serviceProvider.GetService<IStoreProvider>();

            IConfigurationReader configReader =
                new CompositeConfigurationReader(
                    fabricClientFacade);

            string location = ConfigurationManager.AppSettings["geoLocation"];
            string federationId = ConfigurationManager.AppSettings["geoFederationName"];

            ServiceManagerConfiguration serviceManagerConfig =
                await configReader.ReadFabricServiceManagerConfigurationAsync(FabricServiceType.ServerService.ToString(), default(CancellationToken), true, federationId);

            int currentPoolCapacity = serviceManagerConfig.PoolLimits.First().Capacity;
            int currentFreePercentage = serviceManagerConfig.PoolLimits.First().FreePercentageToAllocate;

            UpdateServicePoolManagerConfigWorkflow workflow = new UpdateServicePoolManagerConfigWorkflow(federationId, location, currentFreePercentage - 10, currentPoolCapacity / 2, FabricServiceType.ServerService);
            workflow.SubscriptionId = Guid.NewGuid().ToString();
            ManagementUtil.WaitForOperation(workflow);

            ServiceManagerConfiguration updatedServiceManagerConfig =
                await configReader.ReadFabricServiceManagerConfigurationAsync(FabricServiceType.ServerService.ToString(), default(CancellationToken), true, federationId);

            Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().Capacity, currentPoolCapacity / 2);
            Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().FreePercentageToAllocate, currentFreePercentage - 10);

            using (DocumentClient client = TestCommon.CreateClient(true))
            {
                string databaseName = "geodb-" + Guid.NewGuid();
                CosmosDatabaseSettings db = new Database
                {
                    Id = databaseName,
                };

                ResourceResponse<Database> dbResponse = await client.CreateDatabaseAsync(db);
                Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                string databaseSelfLink = dbResponse.Resource.SelfLink;

                string collectionName = "geocoll-" + Guid.NewGuid();
                CosmosContainerSettings  collection = new DocumentCollection()
                {
                    Id = collectionName,
                };
                ResourceResponse<DocumentCollection> collResponse = await client.CreateDocumentCollectionAsync(databaseSelfLink, collection);
                Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode, "Status code should be Created (201)");
                Util.ValidateCommonCustomHeaders(collResponse.Headers);


                // Try to set capacity to 0
                workflow = new UpdateServicePoolManagerConfigWorkflow(federationId, location, currentFreePercentage, 0, FabricServiceType.ServerService);
                workflow.SubscriptionId = Guid.NewGuid().ToString();
                ManagementUtil.WaitForOperation(workflow);

                await client.ReadDocumentCollectionAsync(collResponse.Resource.SelfLink);

                updatedServiceManagerConfig =
                    await configReader.ReadFabricServiceManagerConfigurationAsync(FabricServiceType.ServerService.ToString(), default(CancellationToken), true, federationId);

                Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().Capacity, 0);
                Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().FreePercentageToAllocate, currentFreePercentage);

                workflow = new UpdateServicePoolManagerConfigWorkflow(federationId, location, currentFreePercentage, currentPoolCapacity, FabricServiceType.ServerService);
                workflow.SubscriptionId = Guid.NewGuid().ToString();
                ManagementUtil.WaitForOperation(workflow);

                updatedServiceManagerConfig =
                    await configReader.ReadFabricServiceManagerConfigurationAsync(FabricServiceType.ServerService.ToString(), default(CancellationToken), true, federationId);

                Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().Capacity, currentPoolCapacity);
                Assert.AreEqual(updatedServiceManagerConfig.PoolLimits.First().FreePercentageToAllocate, currentFreePercentage);
            }
        }

        /// <summary>
        /// Validate write status for a region.
        /// </summary>
        /// <returns></returns>
        private async Task ValidateWriteStatus()
        {
            AdminClient adminClient = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()));
            await adminClient.InitializeAsync();

            string entity1 = @"South Central US";
            string entity2 = @"East US";

            COMMONRM::Topology topologyInput = COMMONRM::TopologyHelper.GenerateNewTopology(writeRegion: entity1);
            COMMONRM::TopologyRequest topologyRequest = new COMMONRM::TopologyRequest()
            {
                Type = COMMONRM::TopologyRequestType.AddReadRegion,
                Directive = COMMONRM::TopologyDirective.Star,
                ReadRegionToAdd = entity2
            };

            topologyInput = COMMONRM::TopologyHelper.GetTransformedTopology(
                topologyInput, topologyRequest);

            using (new ActivityScope(Guid.NewGuid()))
            {
                // save the original topology
                COMMON::Microsoft.Azure.Documents.Topology originalTopology
                    = await adminClient.ReadTopologyResourceAsync(adminClient.MasterServiceIdentity);
                originalTopology.ResourceId = null;

                await adminClient.UpsertTopologyResourceAsync(topologyInput, adminClient.MasterServiceIdentity);
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                // Execute Read Topology
                COMMONRM::Topology topologyRead =
                    await adminClient.ReadTopologyResourceAsync(adminClient.MasterServiceIdentity);

                if (!string.Equals(topologyInput.ToString(), topologyRead.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail(@"Unexpected response from readtopology.");
                }

                // Revoke write status for "South Central US"
                topologyRead.SetMajorIncrementGlobalConfigNumber(topologyRead.GlobalConfigurationNumber);
                await adminClient.RevokeWriteStatusAsync(topologyRead, adminClient.MasterServiceIdentity);
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                // Check Revoked Write Status
                bool bIsWriteStatusRevoked = await adminClient.GetIsWriteStatusRevokedAsync(adminClient.MasterServiceIdentity);
                Assert.AreEqual(true, bIsWriteStatusRevoked, @"Unexpected result on checking write status.");

                // Get E_NOT_WRITE_REGION for a create resource operation.
                // We have chosen to test with partition resource, but any other resource write should trigger the same response.
                PartitionResource partitionResource = null;
                try
                {
                    partitionResource = await adminClient.CreatePartitionResourceAsync(
                        new PartitionResource()
                        {
                            ServiceType = FabricServiceType.MasterService.ToString(),
                            LinkRelationType = (int)LinkRelationTypes.Geo,
                            WellKnownServiceUrl = string.Format(
                                CultureInfo.InvariantCulture, "fabric:/app/svc{0}", Guid.NewGuid().ToString("N").Substring(0, 2)),
                            Region = string.Format(
                                        CultureInfo.InvariantCulture, "{0}", Guid.NewGuid().ToString("N").Substring(0, 6))
                        },
                        adminClient.MasterServiceIdentity);

                    // It should have thrown an exception. so shouldn't reach here
                    Assert.Fail(@"Didn't encounter an exception that was expected for the above create partition resource operation.");
                }
                catch (Exception ex)
                {
                    if (!ex.Message.Contains(@"cannot be performed at this region"))
                    {
                        Assert.Fail(@"Unexpected exception from a write disabled region.");
                    }
                }

                // Grant Write Status back to "South Central US".
                // No particular reason to grant write status on "South Central US" again.
                // WriteStatus can be granted on other regions as well.
                topologyInput.SetMajorIncrementGlobalConfigNumber(topologyRead.GlobalConfigurationNumber);
                await adminClient.GrantWriteStatusAsync(topologyInput, adminClient.MasterServiceIdentity);
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);
                bIsWriteStatusRevoked = await adminClient.GetIsWriteStatusRevokedAsync(adminClient.MasterServiceIdentity);
                Assert.AreEqual(false, bIsWriteStatusRevoked, @"Unexpected result on checking write status.");

                // Now, try the create partition resource operation, it should succeed as a way to check if write is going through.
                partitionResource = await adminClient.CreatePartitionResourceAsync(
                    new PartitionResource()
                    {
                        ServiceType = FabricServiceType.MasterService.ToString(),
                        LinkRelationType = (int)LinkRelationTypes.Geo,
                        WellKnownServiceUrl = string.Format(
                            CultureInfo.InvariantCulture, "fabric:/app/svc{0}", Guid.NewGuid().ToString("N").Substring(0, 2)),
                        Region = string.Format(
                                    CultureInfo.InvariantCulture, "{0}", Guid.NewGuid().ToString("N").Substring(0, 6))
                    },
                    adminClient.MasterServiceIdentity);

                // Execute Read Topology
                topologyRead =
                    await adminClient.ReadTopologyResourceAsync(adminClient.MasterServiceIdentity);

                if (!string.Equals(topologyInput.ToString(), topologyRead.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    Assert.Fail(@"Unexpected response from readtopology.");
                }

                await adminClient.DeletePartitionResourceIfExistsAsync(partitionResource.ResourceId, adminClient.MasterServiceIdentity);

                // restore the original topology and wait for topology get restored
                originalTopology.SetMajorIncrementGlobalConfigNumber(topologyRead.GlobalConfigurationNumber);
                await adminClient.GrantWriteStatusAsync(originalTopology, adminClient.MasterServiceIdentity);
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

            }
        }

        private async Task ValidateCollectionCRUDAsync()
        {
            using (DocumentClient writeRegionClient = TestCommon.CreateClient(true, createForGeoRegion: false))
            {
                using (DocumentClient readRegionClient = TestCommon.CreateClient(true, createForGeoRegion: true, enableEndpointDiscovery: false))
                {
                    string databaseName = "geocollcruddb-" + Guid.NewGuid();
                    CosmosDatabaseSettings db = new Database
                    {
                        Id = databaseName,
                    };

                    ResourceResponse<Database> dbResponse = await writeRegionClient.CreateDatabaseAsync(db);
                    Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                    this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                    string databaseSelfLink = dbResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<Database> readRegionDbResponse = await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                    this.ValidateDatabaseResponseBody(readRegionDbResponse.Resource, databaseName);
                    Assert.AreEqual(dbResponse.Resource.ETag, readRegionDbResponse.Resource.ETag);
                    Assert.AreEqual(dbResponse.Resource.ResourceId, readRegionDbResponse.Resource.ResourceId);
                    Assert.AreEqual(dbResponse.Resource.SelfLink, readRegionDbResponse.Resource.SelfLink);

                    string collectionName = "geocollcrudcoll-" + Guid.NewGuid();
                    CosmosContainerSettings  collection = new DocumentCollection()
                    {
                        Id = collectionName,
                    };
                    ResourceResponse<DocumentCollection> collResponse = await writeRegionClient.CreateDocumentCollectionAsync(databaseSelfLink, collection, new RequestOptions { OfferThroughput = 8000 });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(collResponse.Headers);

                    string collectionSelfLink = collResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<DocumentCollection> readRegionCollResponse = await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink);
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


                    ResourceResponse<DocumentCollection> replaceCollResponse = await writeRegionClient.ReplaceDocumentCollectionAsync(collResponse.Resource);
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
                    CosmosDatabaseSettings db = new Database
                    {
                        Id = databaseName,
                    };

                    ResourceResponse<Database> dbResponse = await writeRegionClient.CreateDatabaseAsync(db);
                    Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                    this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                    string databaseSelfLink = dbResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<Database> readRegionDbResponse = await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                    this.ValidateDatabaseResponseBody(readRegionDbResponse.Resource, databaseName);
                    Assert.AreEqual(dbResponse.Resource.ETag, readRegionDbResponse.Resource.ETag);
                    Assert.AreEqual(dbResponse.Resource.ResourceId, readRegionDbResponse.Resource.ResourceId);
                    Assert.AreEqual(dbResponse.Resource.SelfLink, readRegionDbResponse.Resource.SelfLink);

                    string collectionName = "geocollcrudcoll-" + Guid.NewGuid();
                    CosmosContainerSettings  collection = new DocumentCollection()
                    {
                        Id = collectionName,
                        PartitionKey = new PartitionKeyDefinition
                        {
                            Paths = new Collection<string> { "/id" }
                        }
                    };
                    ResourceResponse<DocumentCollection> collResponse = await writeRegionClient.CreateDocumentCollectionAsync(databaseSelfLink, collection, new RequestOptions { OfferThroughput = 12000 });
                    Assert.AreEqual(HttpStatusCode.Created, collResponse.StatusCode, "Status code should be Created (201)");

                    string collectionSelfLink = collResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<DocumentCollection> readRegionCollResponse = await readRegionClient.ReadDocumentCollectionAsync(collectionSelfLink, new RequestOptions { SessionToken = collResponse.SessionToken });
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


                    ResourceResponse<DocumentCollection> replaceCollResponse = await writeRegionClient.ReplaceDocumentCollectionAsync(collResponse.Resource);

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

        private async Task ValidateMasterCRUDAsync()
        {
            using (DocumentClient writeRegionClient = TestCommon.CreateClient(true, createForGeoRegion: false))
            {
                using (DocumentClient readRegionClient = TestCommon.CreateClient(true, createForGeoRegion: true, enableEndpointDiscovery: false))
                {
                    string databaseName = "geomastercruddb-" + Guid.NewGuid();
                    CosmosDatabaseSettings db = new Database
                    {
                        Id = databaseName,
                    };

                    ResourceResponse<Database> dbResponse = await writeRegionClient.CreateDatabaseAsync(db);
                    Assert.AreEqual(HttpStatusCode.Created, dbResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(dbResponse.Headers);
                    this.ValidateDatabaseResponseBody(dbResponse.Resource, databaseName);

                    string databaseSelfLink = dbResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<Database> readRegionDbResponse = await readRegionClient.ReadDatabaseAsync(databaseSelfLink);
                    this.ValidateDatabaseResponseBody(readRegionDbResponse.Resource, databaseName);
                    Assert.AreEqual(dbResponse.Resource.ETag, readRegionDbResponse.Resource.ETag);
                    Assert.AreEqual(dbResponse.Resource.ResourceId, readRegionDbResponse.Resource.ResourceId);
                    Assert.AreEqual(dbResponse.Resource.SelfLink, readRegionDbResponse.Resource.SelfLink);

                    // Create User
                    string userName = "geodbuser-" + Guid.NewGuid();
                    User user = new User
                    {
                        Id = userName,
                    };
                    ResourceResponse<User> userResponse = await writeRegionClient.CreateUserAsync(databaseSelfLink, user);
                    Assert.AreEqual(HttpStatusCode.Created, userResponse.StatusCode, "Status code should be Created (201)");
                    Util.ValidateCommonCustomHeaders(userResponse.Headers);

                    string userSelfLink = userResponse.Resource.SelfLink;

                    // Add some delay since we do async replication between geo-regions
                    await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                    ResourceResponse<User> userReadRegionResponse = await readRegionClient.ReadUserAsync(userSelfLink);
                    Assert.AreEqual(userName, userReadRegionResponse.Resource.Id);
                    Assert.AreEqual(userResponse.Resource.ETag, userReadRegionResponse.Resource.ETag);
                    Assert.AreEqual(userResponse.Resource.ResourceId, userReadRegionResponse.Resource.ResourceId);
                    Assert.AreEqual(userResponse.Resource.SelfLink, userReadRegionResponse.Resource.SelfLink);

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

        /// <summary>
        /// Exercise the failover APIs (Revoke/InstallWriteStatus, GetCommittedLSNAsync)
        /// </summary>
        /// <returns></returns>
        private async Task ValidateFailoverAPIs()
        {
            AdminClient adminClientWriteRegion = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()));
            AdminClient adminClientReadRegion = TestCommon.CreateAdminClient(new FabricClientFacade(this.GetType().ToString()), "localhost-westus");
            await adminClientWriteRegion.InitializeAsync();
            await adminClientReadRegion.InitializeAsync();

            long lsnMasterServiceWriteRegion = -2;
            long lsnMasterServiceReadRegion = -1;
            using (new ActivityScope(Guid.NewGuid()))
            {
                string entity1 = @"South Central US";
                string entity2 = @"West US";

                // save the original topology
                COMMON::Microsoft.Azure.Documents.Topology originalTopology
                    = await adminClientWriteRegion.ReadTopologyResourceAsync(adminClientWriteRegion.MasterServiceIdentity);
                originalTopology.ResourceId = null;

                COMMON::Microsoft.Azure.Documents.Topology topology
                        = await adminClientWriteRegion.ReadTopologyResourceAsync(adminClientWriteRegion.MasterServiceIdentity);
                topology.ResourceId = null;

                try
                {
                    COMMONRM::Topology topologyInput = COMMONRM::TopologyHelper.GenerateNewTopology(writeRegion: entity1);

                    COMMONRM::TopologyRequest topologyRequest = new COMMONRM::TopologyRequest()
                    {
                        Type = COMMONRM::TopologyRequestType.AddReadRegion,
                        Directive = COMMONRM::TopologyDirective.Star,
                        ReadRegionToAdd = entity2
                    };

                    topologyInput = COMMONRM::TopologyHelper.GetTransformedTopology(topologyInput, topologyRequest);

                    await adminClientWriteRegion.UpsertTopologyResourceAsync(topologyInput, adminClientWriteRegion.MasterServiceIdentity);

                    topology = await adminClientWriteRegion.ReadTopologyResourceAsync(adminClientWriteRegion.MasterServiceIdentity);
                    topology.ResourceId = null;

                    // Revoke Write Status
                    topology.SetMajorIncrementGlobalConfigNumber(topology.GlobalConfigurationNumber);
                    await adminClientWriteRegion.RevokeWriteStatusAsync(topology, adminClientWriteRegion.MasterServiceIdentity);

                    // Get Write status
                    bool bWriteStatusRevoked = await adminClientWriteRegion.GetIsWriteStatusRevokedAsync(adminClientWriteRegion.MasterServiceIdentity);
                    Assert.AreEqual(true, bWriteStatusRevoked, @"Incorrect write status");

                    // Wait for 10 seconds to drain the already enqueued operations.
                    await Task.Delay(10000);

                    // Test that write status is revoked.
                    await VerifyRevokedWriteStatus();

                    // Test for master service.
                    lsnMasterServiceWriteRegion = (await adminClientWriteRegion.GetCommittedLSNAsync(adminClientWriteRegion.MasterServiceIdentity)).CommittedLsn.Value;
                    lsnMasterServiceReadRegion = (await adminClientReadRegion.GetCommittedLSNAsync(adminClientReadRegion.MasterServiceIdentity)).CommittedLsn.Value;

                    // disable the assert for now, as this is not stable due to time sync.
                    //  Assert.AreEqual(lsnMasterServiceReadRegion, lsnMasterServiceWriteRegion, @"LSN didn't converge.");
                    Logger.LogLine("ReadRegion and WriteRegion LSN {0} {1}", lsnMasterServiceReadRegion, lsnMasterServiceWriteRegion);
                }
                finally
                {
                    // make sure we always restore the write status in write region.
                    originalTopology.SetMajorIncrementGlobalConfigNumber(topology.GlobalConfigurationNumber);
                    adminClientWriteRegion.GrantWriteStatusAsync(originalTopology, adminClientWriteRegion.MasterServiceIdentity).Wait();
                }

                bool bWriteStatusRevoked1 = await adminClientWriteRegion.GetIsWriteStatusRevokedAsync(adminClientWriteRegion.MasterServiceIdentity);
                Assert.AreEqual(false, bWriteStatusRevoked1, @"Incorrect write status");

                // and wait for topology get restored so it won't affect the next test.
                await Task.Delay(GlobalDatabaseAccountTests.WaitDurationForAsyncReplication);

                long readRegionXPRole = await adminClientReadRegion.GetXPRoleFromPrimaryAsync(adminClientReadRegion.MasterServiceIdentity);
                long writeRegionXPRole = await adminClientReadRegion.GetXPRoleFromPrimaryAsync(adminClientWriteRegion.MasterServiceIdentity);

                // Validate that the XP role is XP Secondary (4) in read region primary and XP None (1) in the write region primary
                Assert.AreEqual(4, readRegionXPRole, @"Incorrect XP role in read region");
                Assert.AreEqual(1, writeRegionXPRole, @"Incorrect XP role in write region");

                // Evaluate Master CRUD after installing write status.
                await this.ValidateMasterCRUDAsync();
            }
        }

        /// <summary>
        /// Verify write revoked status on the writeRegionEndpointUri
        /// </summary>
        /// <returns></returns>
        private async Task VerifyRevokedWriteStatus()
        {
            HttpResponseMessage responseMessage = await this.CreateDatabaseUsingHttpClient();

            Assert.AreEqual(HttpStatusCode.Forbidden, responseMessage.StatusCode, @"Unexpected error code.");

            if (!responseMessage.Content.ReadAsStringAsync().Result.Contains(
                        @"The requested operation cannot be performed at this region"))
            {
                Assert.Fail(@"Unexpected error returned, when checking for revoked write status.");
            }
        }

        /// <summary>
        /// Create a database using Http Client.
        /// </summary>
        /// <returns></returns>
        private async Task<HttpResponseMessage> CreateDatabaseUsingHttpClient()
        {
            using (HttpClient client = GlobalDatabaseAccountTests.CreateHttpClient(HttpConstants.Versions.CurrentVersion))
            {
                INameValueCollection headers = new StringKeyValueCollection();

                string databaseName = Guid.NewGuid().ToString("N");
                CosmosDatabaseSettings database = new Database
                {
                    Id = databaseName,
                };

                Logger.LogLine("Creating Database");
                client.AddMasterAuthorizationHeader("post", "", "dbs", headers, this.masterKey);

                return await client.PostAsync(new Uri(this.writeRegionEndpointUri, "dbs"), database.AsHttpContent());
            }
        }

        /// <summary>
        /// Create a http client for the given apiVersion.
        /// </summary>
        /// <param name="apiVersion"></param>
        /// <returns></returns>
        private static HttpClient CreateHttpClient(string apiVersion)
        {
            HttpClient client = new HttpClient();

            CacheControlHeaderValue cacheControl = new CacheControlHeaderValue();
            cacheControl.NoCache = true;
            client.DefaultRequestHeaders.CacheControl = cacheControl;

            if (apiVersion != null)
            {
                client.DefaultRequestHeaders.Remove(HttpConstants.HttpHeaders.Version);
                client.DefaultRequestHeaders.Add(HttpConstants.HttpHeaders.Version, apiVersion);
            }

            return client;
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