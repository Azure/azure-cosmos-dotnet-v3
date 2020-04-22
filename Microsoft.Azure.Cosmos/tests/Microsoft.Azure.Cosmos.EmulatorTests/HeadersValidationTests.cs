//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HeadersValidationTests
    {
        [ClassInitialize]
        public static void Initialize(TestContext textContext)
        {
            DocumentClientSwitchLinkExtension.Reset("HeadersValidationTests");
        }

        [TestInitialize]
        public async Task Startup()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true);
            await TestCommon.DeleteAllDatabasesAsync();
        }

        [TestMethod]
        public void ValidatePageSizeHttps()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Https);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Https);
            this.ValidatePageSize(client);
        }

        [TestMethod]
        public void ValidatePageSizeRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidatePageSize(client);
        }

        [TestMethod]
        public void ValidatePageSizeGatway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidatePageSize(client);
        }

        private void ValidatePageSize(DocumentClient client)
        {
            // Invalid parsing
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "\"Invalid header type\"");

            try
            {
                this.ReadDatabaseFeedRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            headers = new DictionaryNameValueCollection();
            headers.Add("pageSize", "\"Invalid header type\"");

            try
            {
                this.ReadFeedScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Invalid value
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "-2");

            try
            {
                this.ReadDatabaseFeedRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, Int64.MaxValue.ToString(CultureInfo.InvariantCulture));

            try
            {
                this.ReadFeedScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Valid page size
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "20");
            DocumentServiceResponse response = this.ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);

            headers = new DictionaryNameValueCollection();
            headers.Add("pageSize", "20");
            StoredProcedureResponse<string> result = this.ReadFeedScript(client, headers);
            Assert.IsTrue(result.StatusCode == HttpStatusCode.OK);

            // dynamic page size
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.PageSize, "-1");
            response = this.ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK);

            headers = new DictionaryNameValueCollection();
            headers.Add("pageSize", "-1");
            result = this.ReadFeedScript(client, headers);
            Assert.IsTrue(result.StatusCode == HttpStatusCode.OK);
        }

        [TestMethod]
        public void ValidateConsistencyLevelGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateCosistencyLevel(client);
        }

        [TestMethod]
        public void ValidateConsistencyLevelRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidateCosistencyLevel(client);
        }

        [TestMethod]
        public void ValidateConsistencyLevelHttps()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Https);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Https);
            this.ValidateCosistencyLevel(client);
        }

        private void ValidateCosistencyLevel(DocumentClient client)
        {
            //this is can be only tested with V2 OM, V3 doesnt allow to set invalid consistencty
            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink, new DocumentCollection
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = partitionKeyDefinition,
            }).Result;

            // Value not supported
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, "Not a valid value");

            try
            {
                this.ReadDocumentFeedRequest(client, collection.ResourceId, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "invalid status code");
            }

            // Supported value
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.ConsistencyLevel, ConsistencyLevel.Eventual.ToString());
            DocumentServiceResponse response = this.ReadDocumentFeedRequest(client, collection.ResourceId, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.OK, "Invalid status code");
        }

        [TestMethod]
        public void ValidateIndexingDirectiveGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateIndexingDirective(client);
        }

        [TestMethod]
        public void ValidateIndexingDirectiveRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidateIndexingDirective(client);
        }

        [TestMethod]
        public void ValidateIndexingDirectiveHttps()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Https);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Https);
            this.ValidateIndexingDirective(client);
        }

        private void ValidateIndexingDirective(DocumentClient client)
        {
            //Will keep this as V2 OM, because we can pass invalid IndexingDirective from V3 onwarsds
            // Number out of range.
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IndexingDirective, "\"Invalid Value\"");

            try
            {
                this.CreateDocumentRequest(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            headers = new DictionaryNameValueCollection();
            headers.Add("indexAction", "\"Invalid Value\"");

            try
            {
                this.CreateDocumentScript(client, headers);
                Assert.Fail("Should throw an exception");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Valid Indexing Directive
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IndexingDirective, IndexingDirective.Exclude.ToString());
            DocumentServiceResponse response = this.CreateDocumentRequest(client, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.Created);

            headers = new DictionaryNameValueCollection();
            headers.Add("indexAction", "\"exclude\"");
            StoredProcedureResponse<string> result = this.CreateDocumentScript(client, headers);
            Assert.IsTrue(result.StatusCode == HttpStatusCode.OK, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEnableScanInQueryGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateEnableScanInQuery(client);
        }

        [TestMethod]
        public void ValidateEnableScanInQueryRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidateEnableScanInQuery(client);
        }

        [TestMethod]
        public void ValidateEnableScanInQueryHttps()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Https);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Https);
            this.ValidateEnableScanInQuery(client);
        }

        private void ValidateEnableScanInQuery(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, "Not a boolean");

            try
            {
                DocumentServiceResponse response = this.ReadDatabaseFeedRequest(client, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP
                    Assert.IsTrue(response.StatusCode == HttpStatusCode.OK, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Valid boolean
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableScanInQuery, "true");
            DocumentServiceResponse response2 = this.ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response2.StatusCode == HttpStatusCode.OK, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEnableLowPrecisionOrderByGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateEnableLowPrecisionOrderBy(client);
        }

        private void ValidateEnableLowPrecisionOrderBy(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, "Not a boolean");

            Document document = this.CreateDocumentRequest(client, new DictionaryNameValueCollection()).GetResource<Document>();
            try
            {
                DocumentServiceResponse response = this.ReadDocumentRequest(client, document, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP"
                    Assert.IsTrue(response.StatusCode == HttpStatusCode.OK, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Valid boolean
            document = this.CreateDocumentRequest(client, new DictionaryNameValueCollection()).GetResource<Document>();
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EnableLowPrecisionOrderBy, "true");
            DocumentServiceResponse response2 = this.ReadDocumentRequest(client, document, headers);
            Assert.IsTrue(response2.StatusCode == HttpStatusCode.OK, "Invalid status code");
        }

        [TestMethod]
        public void ValidateEmitVerboseTracesInQueryGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateEmitVerboseTracesInQuery(client);
        }

        [TestMethod]
        public void ValidateEmitVerboseTracesInQueryRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidateEmitVerboseTracesInQuery(client);
        }

        [TestMethod]
        public void ValidateEmitVerboseTracesInQueryHttps()
        {
            DocumentClient client = TestCommon.CreateClient(false, Protocol.Https);
            this.ValidateEmitVerboseTracesInQuery(client, true);
        }

        private void ValidateEmitVerboseTracesInQuery(DocumentClient client, bool isHttps = false)
        {
            // Value not boolean
            INameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, "Not a boolean");

            try
            {
                DocumentServiceResponse response = this.ReadDatabaseFeedRequest(client, headers);
                if (isHttps)
                {
                    Assert.Fail("Should throw an exception");
                }
                else
                {
                    // Invalid boolean is treated as false by TCP
                    Assert.IsTrue(response.StatusCode == HttpStatusCode.OK, "Invalid status code");
                }
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.BadRequest, "Invalid status code");
            }

            // Valid boolean
            headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.EmitVerboseTracesInQuery, "true");
            DocumentServiceResponse response2 = this.ReadDatabaseFeedRequest(client, headers);
            Assert.IsTrue(response2.StatusCode == HttpStatusCode.OK, "Invalid status code");
        }

        [TestMethod]
        public void ValidateIfNonMatchGateway()
        {
            DocumentClient client = TestCommon.CreateClient(true);
            this.ValidateIfNonMatch(client);

        }
        [TestMethod]
        public void ValidateIfNonMatchHttps()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Https);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Https);
            this.ValidateIfNonMatch(client);
        }

        [TestMethod]
        public void ValidateIfNonMatchRntbd()
        {
            //var client = TestCommon.CreateClient(false, Protocol.Tcp);
            DocumentClient client = TestCommon.CreateClient(true, Protocol.Tcp);
            this.ValidateIfNonMatch(client);
        }

        [TestMethod]
        public void ValidateVersionHeader()
        {
            string correctVersion = HttpConstants.Versions.CurrentVersion;
            try
            {
                DocumentClient client = TestCommon.CreateClient(true);
                Database db = client.CreateDatabaseAsync(new Database() { Id = Guid.NewGuid().ToString() }).Result.Resource;
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };
                DocumentCollection coll = client.CreateDocumentCollectionAsync(db.SelfLink, new DocumentCollection() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition }).Result.Resource;
                Document doc = client.CreateDocumentAsync(coll.SelfLink, new Document()).Result.Resource;
                client = TestCommon.CreateClient(true);
                doc = client.CreateDocumentAsync(coll.SelfLink, new Document()).Result.Resource;
                HttpConstants.Versions.CurrentVersion = "2015-01-01";
                client = TestCommon.CreateClient(true);
                try
                {
                    doc = client.CreateDocumentAsync(coll.SelfLink, new Document()).Result.Resource;
                    Assert.Fail("Should have faild because of version error");
                }
                catch (AggregateException exception)
                {
                    if (exception.InnerException is DocumentClientException dce)
                    {
                        Assert.AreEqual(dce.StatusCode, HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        Assert.Fail("Should have faild because of version error with DocumentClientException BadRequest");
                    }
                }
            }
            finally
            {
                HttpConstants.Versions.CurrentVersion = correctVersion;
            }
        }

        [TestMethod]
        public async Task ValidateCurrentWriteQuorumAndReplicaSetHeader()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false);
            Cosmos.Database db = null;
            try
            {
                db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
                ContainerProperties containerSetting = new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                };
                Container coll = await db.CreateContainerAsync(containerSetting);
                Document documentDefinition = new Document { Id = Guid.NewGuid().ToString() };
                ItemResponse<Document> docResult = await coll.CreateItemAsync<Document>(documentDefinition);
                Assert.IsTrue(int.Parse(docResult.Headers[WFConstants.BackendHeaders.CurrentWriteQuorum], CultureInfo.InvariantCulture) > 0);
                Assert.IsTrue(int.Parse(docResult.Headers[WFConstants.BackendHeaders.CurrentReplicaSetSize], CultureInfo.InvariantCulture) > 0);
            }
            finally
            {
                await db.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task ValidateCollectionIndexProgressHeadersGateway()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(true);
            await this.ValidateCollectionIndexProgressHeaders(client);
        }

        [TestMethod]
        public async Task ValidateCollectionIndexProgressHeadersHttps()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false);
            await this.ValidateCollectionIndexProgressHeaders(client);
        }

        [TestMethod]
        public async Task ValidateCollectionIndexProgressHeadersRntbd()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(false);
            await this.ValidateCollectionIndexProgressHeaders(client);
        }

        private async Task ValidateCollectionIndexProgressHeaders(CosmosClient client)
        {
            Cosmos.Database db = await client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            try
            {
                PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
                ContainerProperties lazyCollection = new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
                lazyCollection.IndexingPolicy.IndexingMode = Cosmos.IndexingMode.Lazy;
                Container lazyContainer = await db.CreateContainerAsync(lazyCollection);

                ContainerProperties consistentCollection = new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
                consistentCollection.IndexingPolicy.IndexingMode = Cosmos.IndexingMode.Consistent;
                Container consistentContainer = await db.CreateContainerAsync(consistentCollection);

                ContainerProperties noneIndexCollection = new ContainerProperties() { Id = Guid.NewGuid().ToString(), PartitionKey = partitionKeyDefinition };
                noneIndexCollection.IndexingPolicy.Automatic = false;
                noneIndexCollection.IndexingPolicy.IndexingMode = Cosmos.IndexingMode.None;
                Container noneIndexContainer = await db.CreateContainerAsync(noneIndexCollection);

                Document doc = new Document() { Id = Guid.NewGuid().ToString() };
                await lazyContainer.CreateItemAsync<Document>(doc);
                await consistentContainer.CreateItemAsync<Document>(doc);
                await noneIndexContainer.CreateItemAsync<Document>(doc);


                // Lazy-indexing collection.
                {
                    ContainerResponse collectionResponse = await lazyContainer.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                    Assert.IsTrue(int.Parse(collectionResponse.Headers[HttpConstants.HttpHeaders.CollectionLazyIndexingProgress], CultureInfo.InvariantCulture) >= 0,
                        "Expect lazy indexer progress when reading lazy collection.");
                    Assert.AreEqual(100, int.Parse(collectionResponse.Headers[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture),
                        "Expect reindexer progress when reading lazy collection.");
                }

                // Consistent-indexing collection.
                {
                    ContainerResponse collectionResponse = await consistentContainer.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                    Assert.IsFalse(collectionResponse.Headers.AllKeys().Contains(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress),
                        "No lazy indexer progress when reading consistent collection.");
                    Assert.AreEqual(100, int.Parse(collectionResponse.Headers[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture),
                        "Expect reindexer progress when reading consistent collection.");
                }

                // None-indexing collection.
                {
                    ContainerResponse collectionResponse = await noneIndexContainer.ReadContainerAsync(requestOptions: new ContainerRequestOptions { PopulateQuotaInfo = true });
                    Assert.IsFalse(collectionResponse.Headers.AllKeys().Contains(HttpConstants.HttpHeaders.CollectionLazyIndexingProgress),
                        "No lazy indexer progress when reading none-index collection.");
                    Assert.AreEqual(100, int.Parse(collectionResponse.Headers[HttpConstants.HttpHeaders.CollectionIndexTransformationProgress], CultureInfo.InvariantCulture),
                        "Expect reindexer progress when reading none-index collection.");
                }
            }
            finally
            {
                await db.DeleteAsync();
            }
        }

        private void ValidateIfNonMatch(DocumentClient client)
        {
            // Valid if-match
            Document document = this.CreateDocumentRequest(client, new DictionaryNameValueCollection()).GetResource<Document>();
            DictionaryNameValueCollection headers = new DictionaryNameValueCollection();
            headers.Add(HttpConstants.HttpHeaders.IfNoneMatch, document.ETag);
            DocumentServiceResponse response = this.ReadDocumentRequest(client, document, headers);
            Assert.IsTrue(response.StatusCode == HttpStatusCode.NotModified, "Invalid status code");

            // validateInvalidIfMatch
            AccessCondition condition = new AccessCondition() { Type = AccessConditionType.IfMatch, Condition = "invalid etag" };
            try
            {
                Document replacedDoc = client.ReplaceDocumentAsync(document.SelfLink, document, new RequestOptions() { AccessCondition = condition }).Result.Resource;
                Assert.Fail("should not reach here");
            }
            catch (Exception ex)
            {
                DocumentClientException innerException = ex.InnerException as DocumentClientException;
                Assert.IsTrue(innerException.StatusCode == HttpStatusCode.PreconditionFailed, "Invalid status code");
            }
        }

        private DocumentServiceResponse ReadDocumentFeedRequest(DocumentClient client, string collectionId, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, collectionId, ResourceType.Document, AuthorizationTokenType.PrimaryMasterKey, headers);

            Range<string> fullRange = new Range<string>(
                PartitionKeyInternal.MinimumInclusiveEffectivePartitionKey,
                PartitionKeyInternal.MaximumExclusiveEffectivePartitionKey,
                true,
                false);
            IRoutingMapProvider routingMapProvider = client.GetPartitionKeyRangeCacheAsync().Result;
            IReadOnlyList<PartitionKeyRange> ranges = routingMapProvider.TryGetOverlappingRangesAsync(collectionId, fullRange).Result;
            request.RouteTo(new PartitionKeyRangeIdentity(collectionId, ranges.First().Id));

            DocumentServiceResponse response = client.ReadFeedAsync(request, null).Result;
            return response;
        }

        private DocumentServiceResponse ReadDatabaseFeedRequest(DocumentClient client, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.ReadFeed, null, ResourceType.Database, AuthorizationTokenType.PrimaryMasterKey, headers);
            DocumentServiceResponse response = client.ReadFeedAsync(request, null).Result;
            return response;
        }

        private StoredProcedureResponse<string> ReadFeedScript(DocumentClient client, INameValueCollection headers)
        {
            var headersIterator = headers.AllKeys().SelectMany(headers.GetValues, (k, v) => new { key = k, value = v });
            string scriptOptions = "{";
            int headerIndex = 0;
            foreach (var header in headersIterator)
            {
                if (headerIndex != 0)
                {
                    scriptOptions += ", ";
                }

                headerIndex++;
                scriptOptions += header.key + ":" + header.value;
            }

            scriptOptions += "}";

            string script = @"function() {
                var client = getContext().getCollection();
                function callback(err, docFeed, responseOptions) {
                    if(err) throw 'Error while reading documents';
                    docFeed.forEach(function(doc, i, arr) { getContext().getResponse().appendBody(JSON.stringify(doc));  });
                };
                client.readDocuments(client.getSelfLink()," + scriptOptions + @", callback);}";

            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            StoredProcedure sproc = new StoredProcedure() { Id = Guid.NewGuid().ToString(), Body = script };
            StoredProcedure createdSproc = client.CreateStoredProcedureAsync(collection, sproc).Result.Resource;
            RequestOptions requestOptions = new RequestOptions();
            requestOptions.PartitionKey = new PartitionKey("test");
            StoredProcedureResponse<string> result = client.ExecuteStoredProcedureAsync<string>(createdSproc, requestOptions).Result;
            return result;
        }

        private DocumentServiceResponse CreateDocumentRequest(DocumentClient client, INameValueCollection headers)
        {
            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            Document document = new Document() { Id = Guid.NewGuid().ToString() };
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Create, collection.SelfLink, document, ResourceType.Document, AuthorizationTokenType.Invalid, headers, SerializationFormattingPolicy.None);
            PartitionKey partitionKey = new PartitionKey(document.Id);
            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, partitionKey.InternalKey.ToJsonString());
            DocumentServiceResponse response = client.CreateAsync(request, null).Result;
            return response;
        }

        private StoredProcedureResponse<string> CreateDocumentScript(DocumentClient client, INameValueCollection headers)
        {
            var headersIterator = headers.AllKeys().SelectMany(headers.GetValues, (k, v) => new { key = k, value = v });
            string scriptOptions = "{";
            int headerIndex = 0;
            foreach (var header in headersIterator)
            {
                if (headerIndex != 0)
                {
                    scriptOptions += ", ";
                }

                headerIndex++;
                scriptOptions += header.key + ":" + header.value;
            }

            scriptOptions += "}";
            string guid = Guid.NewGuid().ToString();

            string script = @" function() {
                var client = getContext().getCollection();                
                client.createDocument(client.getSelfLink(), { id: ""TestDoc"" }," + scriptOptions + @", function(err, docCreated, options) { 
                   if(err) throw new Error('Error while creating document: ' + err.message); 
                   else {
                     getContext().getResponse().setBody(JSON.stringify(docCreated));  
                   }
                });}";

            Database database = client.CreateDatabaseAsync(new Database { Id = Guid.NewGuid().ToString() }).Result;
            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/id" }), Kind = PartitionKind.Hash };
            DocumentCollection collection = client.CreateDocumentCollectionAsync(database.SelfLink,
                new DocumentCollection
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                }).Result;
            StoredProcedure sproc = new StoredProcedure() { Id = Guid.NewGuid().ToString(), Body = script };
            StoredProcedure createdSproc = client.CreateStoredProcedureAsync(collection, sproc).Result.Resource;
            RequestOptions requestOptions = new RequestOptions();
            requestOptions.PartitionKey = new PartitionKey("TestDoc");
            StoredProcedureResponse<string> result = client.ExecuteStoredProcedureAsync<string>(createdSproc, requestOptions).Result;
            return result;
        }

        private DocumentServiceResponse ReadDocumentRequest(DocumentClient client, Document doc, INameValueCollection headers)
        {
            DocumentServiceRequest request = DocumentServiceRequest.Create(OperationType.Read, ResourceType.Document, doc.SelfLink, AuthorizationTokenType.PrimaryMasterKey, headers);
            request.Headers.Set(HttpConstants.HttpHeaders.PartitionKey, new PartitionKey(doc.Id).InternalKey.ToJsonString());
            DocumentServiceResponse retrievedDocResponse = client.ReadAsync(request, null).Result;
            return retrievedDocResponse;
        }
    }
}
