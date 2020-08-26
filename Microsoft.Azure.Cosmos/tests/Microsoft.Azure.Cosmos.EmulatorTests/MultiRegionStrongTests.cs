//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Cosmos.Services.Management.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents;
    using System.Net.Http;

    [TestClass]
    public sealed class MultiRegionStrongTests
    {
        private const string repairInProgressFileName = "ManualRepair.InProgress";

        private string DatabaseName;
        private string CollectionName;

        private DocumentClient read0;
        private DocumentClient read1;
        private DocumentClient read2;

        private DocumentClient write0;
        private DocumentClient write1;
        private DocumentClient write2;


        [TestInitialize]
        public void TestInitialize()
        {
            this.DatabaseName = Guid.NewGuid().ToString();
            this.CollectionName = Guid.NewGuid().ToString();

            this.read0 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: true);
            this.read0.LockClient(0);

            this.read1 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: true);
            this.read1.LockClient(1);

            this.read2 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: true);
            this.read2.LockClient(2);

            this.write0 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: false);
            this.write0.LockClient(0);

            this.write1 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: false);
            this.write1.LockClient(1);

            this.write2 = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: false);
            this.write2.LockClient(2);
        }

        private async Task<DocumentCollection> SetupSingleCollectionScenario()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            await TestCommon.DeleteAllDatabasesAsync();

            Database database = (await client.CreateDatabaseAsync(new Database { Id = this.DatabaseName })).Resource;
            DocumentCollection collection = (await client.CreateDocumentCollectionIfNotExistsAsync(database.SelfLink, new DocumentCollection { Id = this.CollectionName }, new RequestOptions { OfferThroughput = 10000 })).Resource;

            //   await Task.Delay(30000);

            return collection;
        }

        private DocumentClient GetDocumentClient(ConnectionMode connectionMode, Protocol protocol, List<string> preferredRegions)
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy { ConnectionMode = connectionMode, ConnectionProtocol = protocol };
            foreach (string preferredRegion in preferredRegions)
            {
                connectionPolicy.PreferredLocations.Add(preferredRegion);
            }

            return new DocumentClient(
                new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                ConfigurationManager.AppSettings["MasterKey"],
                (HttpMessageHandler)null,
                connectionPolicy);
        }

        private async Task CreateDocumentsAsync(string collectionSelfLink, DocumentClient client, CancellationToken cancellationToken)
        {
            int writeCount = 0;

            int i = 0;
            while (i < 50000)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogLine("WriteCount: {0}", writeCount);
                    return;
                }

                Document doc = (await client.CreateDocumentAsync(collectionSelfLink, new Document { Id = Guid.NewGuid().ToString() })).Resource;
                writeCount++;
                await client.ReadDocumentAsync(doc.SelfLink);

                i++;
            }
        }

        private async Task ReadDocumentsAsync(string collectionSelfLink, DocumentClient client, CancellationToken cancellationToken)
        {
            int readFeedCount = 0;
            int readCount = 0;

            int i = 0;
            while (i < 50000)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogLine("ReadFeedCount: {0}, ReadCount: {1}", readFeedCount, readCount);
                    return;
                }

                DocumentFeedResponse<dynamic> response = await client.ReadDocumentFeedAsync(collectionSelfLink);
                readFeedCount++;

                foreach (var doc in response)
                {
                    await client.ReadDocumentAsync(doc.SelfLink);
                    readCount++;
                }

                i++;
            }
        }

        private void ResumeReplicas(string documentId)
        {
            ResourceId documentRId = ResourceId.Parse(documentId);
            string collectionId = documentRId.DocumentCollectionId.ToString();
            IEnumerable<string> indexFiles = Directory.EnumerateFiles("c:\\wfroot\\", "*.bwdata", SearchOption.AllDirectories);
            List<string> collectionIndexFiles = new List<string>();

            foreach (string indexFile in indexFiles)
            {
                if (indexFile.Contains(collectionId))
                {
                    collectionIndexFiles.Add(indexFile);
                }
            }

            foreach (string fileName in collectionIndexFiles)
            {
                string directoryName = Path.GetDirectoryName(fileName);
                string manualRecoveryFileFullName = Path.Combine(directoryName, MultiRegionStrongTests.repairInProgressFileName);
                try
                {
                    File.Delete(manualRecoveryFileFullName);
                }
                catch (FileNotFoundException)
                {
                }
            }
        }
        /*
        private async Task TestGlobalStrongAsync(ConnectionMode connectionMode, Protocol protocol)
        {
            await TestCommon.DeleteAllDatabasesAsync(TestCommon.CreateClient(true));
            using (await TestCommon.OverrideGlobalDatabaseAccountConfigurationsAsync(Tuple.Create<string, object>("defaultConsistencyLevel", "Strong")))
            {
                await Task.Delay(TimeSpan.FromSeconds(35));

                ConnectionPolicy scusConnectionPolicy = new ConnectionPolicy { ConnectionMode = connectionMode, ConnectionProtocol = protocol };
                scusConnectionPolicy.PreferredLocations.Add("South Central US");

                ConnectionPolicy wusConnectionPolicy = new ConnectionPolicy { ConnectionMode = connectionMode, ConnectionProtocol = protocol };
                wusConnectionPolicy.PreferredLocations.Add("West US");

                DocumentClient scusClient = new DocumentClient(
                    new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                    ConfigurationManager.AppSettings["MasterKey"],
                    scusConnectionPolicy);

                DocumentClient wusClient = new DocumentClient(
                    new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]),
                    ConfigurationManager.AppSettings["MasterKey"],
                    wusConnectionPolicy);

                Database database = (await scusClient.CreateDatabaseIfNotExistsAsync(new Database { Id = "database" })).Resource;
                DocumentCollection collection = (await scusClient.CreateDocumentCollectionIfNotExistsAsync(database.SelfLink, new DocumentCollection { Id = "collection" }, new RequestOptions { OfferThroughput = 10000 })).Resource;
                Document doc = (await scusClient.CreateDocumentAsync(collection.SelfLink, new Document { Id = "lockDoc" })).Resource;
                await Task.Delay(TimeSpan.FromSeconds(30));

                CancellationTokenSource tokenSource = new CancellationTokenSource();
                Task writeTask = Task.Factory.StartNew(async () => await CreateDocumentsAsync(collection.SelfLink, scusClient, tokenSource.Token));
                Task readTask1 = Task.Factory.StartNew(async () => await ReadDocumentsAsync(collection.SelfLink, scusClient, tokenSource.Token));
                Task readTask2 = Task.Factory.StartNew(async () => await ReadDocumentsAsync(collection.SelfLink, wusClient, tokenSource.Token));

                for (uint i = 0; i < 10; i++)
                {
                    bool isReadRegion = i % 2 == 1;
                    uint replicaIndexToLock = i % 3;
                    DocumentClient crashClient = TestCommon.CreateClient(false, enableEndpointDiscovery: false, tokenType: AuthorizationTokenType.SystemAll, createForGeoRegion: isReadRegion);
                    crashClient.LockClient(replicaIndexToLock);

                    try
                    {
                        await crashClient.CrashAsync(doc.ResourceId, typeof(Document));
                    }
                    catch (DocumentClientException ex)
                    {
                        Logger.LogLine("Hit exception {0} while crashing replica index {1}, isReadRegion: {2}", ex.ToString(), replicaIndexToLock, isReadRegion);
                    }
                }

                tokenSource.Cancel();
                await writeTask;
                await readTask1;
                await readTask2;

                Assert.IsTrue(!this.isWriteExceptionCaught);
                Assert.IsTrue(!this.isReadExceptionCaught);
            }
        }

        private async Task CreateDocumentsAsync(string collectionSelfLink, DocumentClient client, CancellationToken cancellationToken)
        {
            this.isWriteExceptionCaught = false;
            int writeCount = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogLine("WriteCount: {0}", writeCount);
                    return;
                }

                try
                {
                    await client.CreateDocumentAsync(collectionSelfLink, new Document { Id = Guid.NewGuid().ToString() });
                    writeCount++;
                }
                catch (DocumentClientException ex)
                {
                    Logger.LogLine("Hit exception {0} while creating document.", ex.ToString());
                    this.isWriteExceptionCaught = true;
                }
            }
        }

        private async Task ReadDocumentsAsync(string collectionSelfLink, DocumentClient client, CancellationToken cancellationToken)
        {
            this.isReadExceptionCaught = false;
            int readFeedCount = 0;
            int readCount = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Logger.LogLine("ReadFeedCount: {0}, ReadCount: {1}", readFeedCount, readCount);
                    return;
                }

                string continuationToken = null;
                try
                {
                    DoucmentFeedResponse<dynamic> response = await client.ReadDocumentFeedAsync(collectionSelfLink, new FeedOptions { RequestContinuation = continuationToken });
                    readFeedCount++;
                    continuationToken = response.ResponseContinuation;

                    foreach(var doc in response)
                    {
                        await client.ReadDocumentAsync(doc.SelfLink);
                        readCount++;
                    }
                }
                catch (DocumentClientException ex)
                {
                    Logger.LogLine("Hit exception {0} while reading document.", ex.ToString());
                    this.isReadExceptionCaught = true;
                }
            }
        }
            */
    }
}
