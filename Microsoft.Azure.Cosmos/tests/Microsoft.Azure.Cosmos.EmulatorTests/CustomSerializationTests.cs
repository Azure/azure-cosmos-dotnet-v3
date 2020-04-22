//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Utils;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    [TestClass]
    public abstract class CustomSerializationTests
    {
        private const string PartitionKeyProperty = "pk";
        private readonly Uri hostUri;
        private readonly string masterKey;
        private readonly DocumentClient documentClient;
        private string databaseName;
        private string collectionName;
        private string partitionedCollectionName;
        private Uri databaseUri;
        private Uri collectionUri;
        private Uri partitionedCollectionUri;
        private PartitionKeyDefinition defaultPartitionKeyDefinition = new PartitionKeyDefinition { Paths = new System.Collections.ObjectModel.Collection<string>(new[] { "/pk" }), Kind = PartitionKind.Hash };

        internal abstract DocumentClient CreateDocumentClient(
            Uri hostUri,
            string key,
            JsonSerializerSettings settings,
            ConnectionPolicy connectionPolicy,
            ConsistencyLevel consistencyLevel);

        internal abstract FeedOptions ApplyFeedOptions(FeedOptions feedOptions, JsonSerializerSettings settings);

        internal abstract RequestOptions ApplyRequestOptions(RequestOptions readOptions, JsonSerializerSettings settings);

        protected CustomSerializationTests()
        {
            this.hostUri = new Uri(ConfigurationManager.AppSettings["GatewayEndpoint"]);
            this.masterKey = ConfigurationManager.AppSettings["MasterKey"];
            this.documentClient = TestCommon.CreateClient(true);
        }

        [TestInitialize]
        public void TestSetup()
        {
            this.databaseName = ConfigurationManager.AppSettings["DatabaseAccountId"];
            this.collectionName = Guid.NewGuid().ToString();
            this.partitionedCollectionName = Guid.NewGuid().ToString();

            this.databaseUri = UriFactory.CreateDatabaseUri(this.databaseName);
            this.collectionUri = UriFactory.CreateDocumentCollectionUri(this.databaseName, this.collectionName);
            this.partitionedCollectionUri = UriFactory.CreateDocumentCollectionUri(this.databaseName, this.partitionedCollectionName);

            Database database = this.documentClient.CreateDatabaseIfNotExistsAsync(new Database() { Id = databaseName }).Result.Resource;

            DocumentCollection newCollection = new DocumentCollection() { Id = collectionName, PartitionKey = defaultPartitionKeyDefinition };
            try
            {
                this.documentClient.CreateDocumentCollectionAsync(this.databaseUri, newCollection, new RequestOptions { OfferThroughput = 400 }).Wait();
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    // Emulator con sometimes fail under load, so we retry
                    Task.Delay(1000);
                    this.documentClient.CreateDocumentCollectionAsync(this.databaseUri, newCollection, new RequestOptions { OfferThroughput = 400 }).Wait();
                }
            }

            DocumentCollection partitionedCollection = new DocumentCollection()
            {
                Id = partitionedCollectionName,
                PartitionKey = new PartitionKeyDefinition()
                {
                    Paths = new Collection<string> { "/pk" },
                }
            };

            try
            {
                this.documentClient.CreateDocumentCollectionAsync(this.databaseUri, partitionedCollection, new RequestOptions { OfferThroughput = 10000 }).Wait();
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    // Emulator con sometimes fail under load, so we retry
                    Task.Delay(1000);
                    this.documentClient.CreateDocumentCollectionAsync(this.databaseUri, partitionedCollection, new RequestOptions { OfferThroughput = 10000 }).Wait();
                }
            }
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await TestCommon.DeleteAllDatabasesAsync();
        }

        [TestMethod]
        public void TestOrderByQuery()
        {
            this.TestOrderyByQueryAsync().GetAwaiter().GetResult();
        }

        [TestMethod]
        public void TestDateParseHandlingOnReadDocument()
        {
            const string jsonProperty = "jsonString";

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            };

            this.SetupDateTimeScenario(serializerSettings, jsonProperty, out DocumentClient client, out Document originalDocument, out Document createdDocument, out Document partitionedDocument);

            // Verify round-trip create and read document
            RequestOptions applyRequestOptions = this.ApplyRequestOptions(new RequestOptions(), serializerSettings);

            this.AssertPropertyOnReadDocument(client, this.collectionUri, createdDocument, applyRequestOptions, originalDocument, jsonProperty);
            this.AssertPropertyOnReadDocument(client, this.partitionedCollectionUri, partitionedDocument, applyRequestOptions, originalDocument, jsonProperty);
        }

        private void AssertPropertyOnReadDocument(
            DocumentClient client,
            Uri targetCollectionUri,
            Document createdDocument,
            RequestOptions requestOptions,
            Document originalDocument,
            string jsonProperty)
        {
            requestOptions.PartitionKey = new PartitionKey(originalDocument.GetPropertyValue<string>(PartitionKeyProperty));

            Document readDocument = client.ReadDocumentAsync(createdDocument.SelfLink, requestOptions).Result.Resource;
            Assert.AreEqual(originalDocument.GetValue<string>(jsonProperty), createdDocument.GetValue<string>(jsonProperty));
            Assert.AreEqual(originalDocument.GetValue<string>(jsonProperty), readDocument.GetValue<string>(jsonProperty));
        }

        [TestMethod]
        [Ignore] // Need to use v3 pipeline
        public void TestDateParseHandlingOnDocumentQuery()
        {
            const string jsonProperty = "jsonString";

            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            };

            this.SetupDateTimeScenario(serializerSettings, jsonProperty, out DocumentClient client, out Document originalDocument, out Document createdDocument, out Document partitionedDocument);

            FeedOptions options = this.ApplyFeedOptions(new FeedOptions() { EnableCrossPartitionQuery = true }, serializerSettings);

            // Verify with query 
            string selectFromCOrderByCTs = "select * from c order by c._ts";

            this.AssertDocumentPropertyOnQuery(client, this.collectionUri, selectFromCOrderByCTs, options, originalDocument, jsonProperty);
            this.AssertDocumentPropertyOnQuery(client, this.partitionedCollectionUri, selectFromCOrderByCTs, options, originalDocument, jsonProperty);
        }

        [TestMethod]
        public void TestDateParseHandling()
        {
            const string jsonProperty = "jsonString";


            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None
            };

            this.SetupDateTimeScenario(serializerSettings, jsonProperty, out DocumentClient client, out Document originalDocument, out Document createdDocument, out Document partitionedDocument);

            // Verify with stored procedure
            StoredProcedure storedProcedure = new StoredProcedure();
            storedProcedure.Id = "storeProcedure1";
            storedProcedure.Body = @"function ReadAll(prefix) {
            var collection = getContext().getCollection();
            var responseBody = {
                createdDocuments: []
            }
            // Query documents and take 1st item.
            var isAccepted = collection.queryDocuments(
                collection.getSelfLink(),
                'select * from root r',
                function(err, feed, options) {
                        if (err) throw err;

                        responseBody.createdDocuments.push(feed[0]);
                        getContext().getResponse().setBody(responseBody);
                    });

                    if (!isAccepted) throw new Error('The query was not accepted by the server.');
                } ";

            RequestOptions applyRequestOptions = this.ApplyRequestOptions(new RequestOptions(), serializerSettings);
            applyRequestOptions.PartitionKey = new PartitionKey("test");
            this.AssertPropertyOnStoredProc(client, this.collectionUri, storedProcedure, applyRequestOptions, originalDocument, jsonProperty);
            this.AssertPropertyOnStoredProc(client, this.partitionedCollectionUri, storedProcedure, applyRequestOptions, originalDocument, jsonProperty);
        }

        private class SprocTestPayload
        {
            public string V1 { get; set; }
        }

        [TestMethod]
        public async Task TestStoredProcJsonSerializerSettings()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
            };

            StoredProcedure storedProcedureDef = new StoredProcedure();
            storedProcedureDef.Id = "testStoredProcJsonSerializerSettings" + Guid.NewGuid().ToString("N");
            storedProcedureDef.Body = @"function() {
                    var docToReturn = {
                        id: 1
                    };

                    __.response.setBody(docToReturn);
                }";

            DocumentClient client = new DocumentClient(this.hostUri, this.masterKey, serializerSettings);

            StoredProcedure sproc = await client.CreateStoredProcedureAsync(this.collectionUri, storedProcedureDef);

            try
            {
                await client.ExecuteStoredProcedureAsync<SprocTestPayload>(sproc.SelfLink, new RequestOptions { PartitionKey = new PartitionKey("value") });
                Assert.Fail();
            }
            catch (SerializationException e)
            {
                Assert.IsTrue(e.Message.Contains("Could not find member 'id' on object of type"));
            }
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task  TestJsonSerializerSettings(bool useGateway)
        {
            CosmosClient cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) => {
                if (useGateway)
                {
                    cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(CustomSerializationTests.GetSerializerWithCustomConverterAndBinder())).WithConnectionModeGateway();
                } else
                {
                    cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(CustomSerializationTests.GetSerializerWithCustomConverterAndBinder())).WithConnectionModeDirect();

                }
            });
            Container container = cosmosClient.GetContainer(this.databaseName, this.partitionedCollectionName);

            Random rnd = new Random();
            byte[] bytes = new byte[100];
            rnd.NextBytes(bytes);
            TestDocument testDocument = new TestDocument(new KerberosTicketHashKey(bytes));

            //create and read
            ItemResponse<TestDocument> createResponse = await container.CreateItemAsync<TestDocument>(testDocument);
            ItemResponse<TestDocument> readResponse = await container.ReadItemAsync<TestDocument>(testDocument.Id, new Cosmos.PartitionKey(testDocument.Name));
            this.AssertEqual(testDocument, readResponse.Resource);
            this.AssertEqual(testDocument, createResponse.Resource);

            // upsert
            ItemResponse<TestDocument> upsertResponse = await container.UpsertItemAsync<TestDocument>(testDocument);
            readResponse = await container.ReadItemAsync<TestDocument>(testDocument.Id, new Cosmos.PartitionKey(testDocument.Name));
            this.AssertEqual(testDocument, readResponse.Resource);
            this.AssertEqual(testDocument, upsertResponse.Resource);

            // replace 
            ItemResponse<TestDocument> replacedResponse = await container.ReplaceItemAsync<TestDocument>(testDocument, testDocument.Id);
            readResponse = await container.ReadItemAsync<TestDocument>(testDocument.Id, new Cosmos.PartitionKey(testDocument.Name));
            this.AssertEqual(testDocument, readResponse.Resource);
            this.AssertEqual(testDocument, replacedResponse.Resource);

            QueryDefinition sql = new QueryDefinition("select * from r");
            FeedIterator<TestDocument> feedIterator =
               container.GetItemQueryIterator<TestDocument>(queryDefinition: sql, requestOptions: new QueryRequestOptions() { MaxItemCount = 1 });

            List<TestDocument> allDocuments = new List<TestDocument>();
            while (feedIterator.HasMoreResults)
            {
                allDocuments.AddRange(await feedIterator.ReadNextAsync());
            }

            this.AssertEqual(testDocument, allDocuments.First());

            //Will add LINQ test once it is available with new V3 OM 
            // // LINQ Lambda
            // var query1 = client.CreateDocumentQuery<TestDocument>(partitionedCollectionUri, options)
            //            .Where(_ => _.Id.CompareTo(String.Empty) > 0)
            //            .Select(_ => _.Id);
            // string query1Str = query1.ToString();
            // var result = query1.ToList();
            // Assert.AreEqual(1, result.Count);
            // Assert.AreEqual(testDocument.Id, result[0]);

            // // LINQ Query
            // var query2 =
            //     from f in client.CreateDocumentQuery<TestDocument>(partitionedCollectionUri, options)
            //     where f.Id.CompareTo(String.Empty) > 0
            //     select f.Id;
            // string query2Str = query2.ToString();
            // var result2 = query2.ToList();
            // Assert.AreEqual(1, result2.Count);
            // Assert.AreEqual(testDocument.Id, result2[0]);
        }

        [TestMethod]
        public void TestStoredProcedure()
        {
            // Create a document client with a customer json serializer settings
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings();
            serializerSettings.Converters.Add(new ObjectStringJsonConverter<SerializedObject>(_ => _.Name, _ => SerializedObject.Parse(_)));
            ConnectionPolicy connectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway };
            ConsistencyLevel defaultConsistencyLevel = ConsistencyLevel.Session;
            DocumentClient client = this.CreateDocumentClient(
                this.hostUri,
                this.masterKey,
                serializerSettings,
                connectionPolicy,
                defaultConsistencyLevel);

            // Create a simple stored procedure
            string scriptId = "bulkImportScript";
            StoredProcedure sproc = new StoredProcedure
            {
                Id = scriptId,
                Body = @"
function bulkImport(docs) {
    var collection = getContext().getCollection();
    var collectionLink = collection.getSelfLink();

    // The count of imported docs, also used as current doc index.
    var count = 0;

    // Validate input.
    if (!docs) throw new Error(""The array is undefined or null."");

    var docsLength = docs.length;
            if (docsLength == 0)
            {
                getContext().getResponse().setBody(0);
            }

            // Call the CRUD API to create a document.
            tryCreate(docs[count], callback);

            // Note that there are 2 exit conditions:
            // 1) The createDocument request was not accepted. 
            //    In this case the callback will not be called, we just call setBody and we are done.
            // 2) The callback was called docs.length times.
            //    In this case all documents were created and we don't need to call tryCreate anymore. Just call setBody and we are done.
            function tryCreate(doc, callback) {
            // If you are sure that every document will contain its own (unique) id field then
            // disable the option to auto generate ids.
            // by leaving this on, the entire document is parsed to check if there is an id field or not
            // by disabling this, parsing of the document is skipped because you're telling DocumentDB 
            // that you are providing your own ids.
            // depending on the size of your documents making this change can have a significant 
            // improvement on document creation. 
            var options = {
            disableAutomaticIdGeneration: true
        };

        var isAccepted = collection.createDocument(collectionLink, doc, options, callback);

        // If the request was accepted, callback will be called.
        // Otherwise report current count back to the client, 
        // which will call the script again with remaining set of docs.
        // This condition will happen when this stored procedure has been running too long
        // and is about to get cancelled by the server. This will allow the calling client
        // to resume this batch from the point we got to before isAccepted was set to false
        if (!isAccepted) getContext().getResponse().setBody(count);
    }

    // This is called when collection.createDocument is done and the document has been persisted.
    function callback(err, doc, options)
    {
        if (err) throw err;

        // One more document has been inserted, increment the count.
        count++;

        if (count >= docsLength)
        {
            // If we have created all documents, we are done. Just set the response.
            getContext().getResponse().setBody(count);
        }
        else
        {
            // Create next document.
            tryCreate(docs[count], callback);
        }
    }
}
"
            };

            sproc = client.CreateStoredProcedureAsync(this.collectionUri, sproc).Result.Resource;

            MyObject doc = new MyObject(1);

            dynamic[] args = new dynamic[] { new dynamic[] { doc } };

            RequestOptions requestOptions = this.ApplyRequestOptions(new RequestOptions { PartitionKey = new PartitionKey("value") }, serializerSettings);

            StoredProcedureResponse<int> scriptResult = client.ExecuteStoredProcedureAsync<int>(
                sproc.SelfLink,
                requestOptions,
                args).Result;

            Uri docUri = UriFactory.CreateDocumentUri(this.databaseName, this.collectionName, doc.id);
            MyObject readDoc = client.ReadDocumentAsync<MyObject>(docUri, requestOptions).Result.Document;
            Assert.IsNotNull(readDoc.SerializedObject);
            Assert.AreEqual(doc.SerializedObject.Name, readDoc.SerializedObject.Name);
        }

        private async Task TestOrderyByQueryAsync()
        {
            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings
            {
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                Converters =
                {
                    new ObjectStringJsonConverter<SerializedObject>(_ => _.Name, _ => SerializedObject.Parse(_))
                }
            };

            CosmosClient cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) => cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(jsonSerializerSettings)));
            Container container = cosmosClient.GetContainer(this.databaseName, this.partitionedCollectionName);

            // Create a few test documents
            int documentCount = 3;
            string numberFieldName = "NumberField";
            for (int i = 0; i < documentCount; ++i)
            {
                MyObject newDocument = new MyObject(i);
                ItemResponse<MyObject> createdDocument = await container.CreateItemAsync<MyObject>(newDocument);
            }

            QueryDefinition cosmosSqlQueryDefinition1 = new QueryDefinition("SELECT * FROM root");
            FeedIterator<MyObject> setIterator1 = container.GetItemQueryIterator<MyObject>(cosmosSqlQueryDefinition1, requestOptions: new QueryRequestOptions { MaxConcurrency = -1, MaxItemCount =-1 });

            QueryDefinition cosmosSqlQueryDefinition2 = new QueryDefinition("SELECT * FROM root ORDER BY root[\"" + numberFieldName + "\"] DESC");
            FeedIterator<MyObject> setIterator2 = container.GetItemQueryIterator<MyObject>(cosmosSqlQueryDefinition2, requestOptions: new QueryRequestOptions { MaxConcurrency = -1, MaxItemCount = -1 });

            List<MyObject> list1 = new List<MyObject>();
            List<MyObject> list2 = new List<MyObject>();

            while (setIterator1.HasMoreResults)
            {
                foreach (MyObject obj in await setIterator1.ReadNextAsync())
                {
                    list1.Add(obj);
                }
            }

            while (setIterator2.HasMoreResults)
            {
                foreach (MyObject obj in await setIterator2.ReadNextAsync())
                {
                    list2.Add(obj);
                }
            }

            Assert.AreEqual(documentCount, list1.Count);
            Assert.AreEqual(documentCount, list2.Count);
            for (int i = 0; i < documentCount; ++i)
            {
                Assert.AreEqual("Name: " + (documentCount - i - 1), list2[i].SerializedObject.Name);
            }
        }

        private void AssertPropertyOnStoredProc(
            DocumentClient client,
            Uri targetCollectionUri,
            StoredProcedure storedProcedure,
            RequestOptions requestOptions,
            Document originalDocument,
            string jsonProperty)
        {
            requestOptions.PartitionKey = new PartitionKey(originalDocument.GetPropertyValue<string>(CustomSerializationTests.PartitionKeyProperty));

            StoredProcedure sproc = client.CreateStoredProcedureAsync(targetCollectionUri, storedProcedure).Result;

            IEnumerable<Document> spResult = client.ExecuteStoredProcedureAsync<Document>(sproc.SelfLink, requestOptions)
                .Result.Response.GetPropertyValue<IEnumerable<Document>>("createdDocuments");

            foreach (Document d in spResult)
            {
                PlayDocument playDoc = (PlayDocument)(dynamic)d;
                Assert.AreEqual(originalDocument.GetValue<string>(jsonProperty), playDoc.jsonString);
            }
        }

        private void AssertDocumentPropertyOnQuery(DocumentClient client, Uri targetCollectionUri, string selectFromCOrderByCTs, FeedOptions options, Document originalDocument, string jsonProperty)
        {
            IQueryable<PlayDocument> iQueryable = client.CreateDocumentQuery<PlayDocument>(
                targetCollectionUri,
                selectFromCOrderByCTs,
                options);

            List<PlayDocument> queryResult = iQueryable.ToList();
            Assert.AreEqual(1, queryResult.Count);
            Assert.AreEqual(originalDocument.GetValue<string>(jsonProperty), queryResult[0].jsonString);
        }

        private void AssertEqual(TestDocument expected, TestDocument actual)
        {
            Assert.AreEqual(expected.Id, actual.Id);
            Assert.AreEqual(expected.KerberosTicketHashKey, actual.KerberosTicketHashKey);
            Assert.AreEqual(expected.KerberosTicketHashKey.GetHashCode(), actual.KerberosTicketHashKey.GetHashCode());
            Assert.AreEqual(expected.Dic1[1], actual.Dic1[1]);
            Assert.AreEqual(expected.Dic1[1].GetHashCode(), actual.Dic1[1].GetHashCode());
            Assert.AreEqual(expected.Dic2, actual.Dic2);
            Assert.AreEqual(expected.Dic1.Count, actual.Dic1.Count);
            Assert.AreEqual(expected.Name, actual.Name);
        }

        private void SetupDateTimeScenario(JsonSerializerSettings serializerSettings, string jsonPropertyName, out DocumentClient client, out Document originalDocument, out Document outputDocument, out Document outputPartitionedDocument)
        {
            ConnectionPolicy connectionPolicy = new ConnectionPolicy { ConnectionMode = ConnectionMode.Gateway };
            ConsistencyLevel defaultConsistencyLevel = ConsistencyLevel.Session;
            client = this.CreateDocumentClient(
                this.hostUri,
                this.masterKey,
                serializerSettings,
                connectionPolicy,
                defaultConsistencyLevel);
            originalDocument = new Document();
            originalDocument.SetPropertyValue(jsonPropertyName, "2017-05-18T17:17:32.7514920Z");
            originalDocument.SetPropertyValue(PartitionKeyProperty, "value");
            outputDocument = client.CreateDocumentAsync(this.collectionUri, originalDocument, this.ApplyRequestOptions(new RequestOptions(), serializerSettings), disableAutomaticIdGeneration: false).Result.Resource;
            outputPartitionedDocument = client.CreateDocumentAsync(this.partitionedCollectionUri, originalDocument, this.ApplyRequestOptions(new RequestOptions(), serializerSettings), disableAutomaticIdGeneration: false).Result.Resource;
        }

#pragma warning disable CS0618 
        public static JsonSerializerSettings GetSerializerWithCustomConverterAndBinder()
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                MissingMemberHandling = MissingMemberHandling.Ignore
            };

            serializerSettings.Binder = new CommonSerializationBinder();
            serializerSettings.Converters =
                serializerSettings.Converters.Concat(
                    new JsonConverter[]
                    {
                        new ObjectJsonConverter<KerberosTicketHashKey, byte[]>(_ => _.KerberosTicketHash, _ => new KerberosTicketHashKey(_)),
                    }).ToList();
            serializerSettings.TypeNameHandling = TypeNameHandling.All;
            serializerSettings.TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Full;
            serializerSettings.DateParseHandling = DateParseHandling.None;
            return serializerSettings;
        }
#pragma warning restore CS0618

        private class CustomJsonSerializer : CosmosSerializer
        {
            private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
            private JsonSerializer serializer;
            public CustomJsonSerializer(JsonSerializerSettings jsonSerializerSettings)
            {
                this.serializer = JsonSerializer.Create(jsonSerializerSettings);
            }
            public override T FromStream<T>(Stream stream)
            {
                using (stream)
                {
                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    using (StreamReader sr = new StreamReader(stream))
                    {
                        using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                        {
                            return this.serializer.Deserialize<T>(jsonTextReader);
                        }
                    }
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: CustomJsonSerializer.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        this.serializer.Serialize(writer, input);
                        writer.Flush();
                        streamWriter.Flush();
                    }
                }

                streamPayload.Position = 0;
                return streamPayload;
            }
        }
    

        [TestClass]
        public sealed class OnDocumentClientTests : CustomSerializationTests
        {
            internal override DocumentClient CreateDocumentClient(
                Uri hostUri,
                string key,
                JsonSerializerSettings settings,
                ConnectionPolicy connectionPolicy,
                ConsistencyLevel consistencyLevel)
            {
                return new DocumentClient(
                    hostUri,
                    key,
                    settings,
                    connectionPolicy,
                    consistencyLevel);
            }

            internal override FeedOptions ApplyFeedOptions(FeedOptions feedOptions, JsonSerializerSettings settings)
            {
                return feedOptions;
            }

            internal override RequestOptions ApplyRequestOptions(RequestOptions readOptions, JsonSerializerSettings settings)
            {
                return readOptions;
            }
        }

        [TestClass]
        public sealed class OnRequestTests : CustomSerializationTests
        {
            internal override DocumentClient CreateDocumentClient(
                Uri hostUri,
                string key,
                JsonSerializerSettings settings,
                ConnectionPolicy connectionPolicy,
                ConsistencyLevel consistencyLevel)
            {
                return new DocumentClient(
                    hostUri,
                    key,
                    (HttpMessageHandler)null,
                    connectionPolicy,
                    consistencyLevel);
            }

            internal override FeedOptions ApplyFeedOptions(FeedOptions feedOptions, JsonSerializerSettings settings)
            {
                feedOptions.JsonSerializerSettings = settings;
                return feedOptions;
            }

            internal override RequestOptions ApplyRequestOptions(RequestOptions readOptions, JsonSerializerSettings settings)
            {
                readOptions.JsonSerializerSettings = settings;
                return readOptions;
            }
        }

        public class ObjectJsonConverter<TSource, TDestination> : JsonConverter
        {
            protected Func<TDestination, TSource> Deserializer;
            protected Func<TSource, TDestination> Serializer;

            public ObjectJsonConverter(
                Func<TSource, TDestination> serializer,
                Func<TDestination, TSource> deserializer)
            {
                this.Serializer = serializer;
                this.Deserializer = deserializer;
            }

            public override bool CanConvert(Type valueType)
            {
                return valueType == typeof(TSource);
            }

            public override void WriteJson(
                JsonWriter writer,
                object value,
                JsonSerializer serializer)
            {
                if (value == null)
                    writer.WriteNull();
                else
                    serializer.Serialize(writer, this.Serializer((TSource)value));
            }

            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                return reader.TokenType == JsonToken.Null
                    ? default
                    : this.Deserializer(serializer.Deserialize<TDestination>(reader));
            }
        }

#pragma warning disable CS0618 
        private sealed class CommonSerializationBinder : Newtonsoft.Json.SerializationBinder
#pragma warning restore CS0618 
        {
            private readonly ConcurrentDictionary<Type, string> _typeToNameMapping;
            private readonly ConcurrentDictionary<string, Type> _nameToTypeMapping;

            public CommonSerializationBinder()
            {
                this._typeToNameMapping = new ConcurrentDictionary<Type, string>();
                this._nameToTypeMapping = new ConcurrentDictionary<string, Type>();
            }

            public override Type BindToType(string assemblyName, string typeName)
            {
                if (assemblyName == null)
                {
                    Type type = null;
                    try
                    {
                        type = this._nameToTypeMapping[typeName];
                    }
                    catch (Exception e)
                    {
                        if (e != null)
                        {
                            throw;
                        }
                    }
                    if (type != null)
                    {
                        return type;
                    }
                }

                return Type.GetType(string.Format("{0}, {1}", typeName, assemblyName), true);
            }

            public override void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                assemblyName = null;
                typeName = this._typeToNameMapping.GetOrAdd(serializedType, _ =>
                {
                    return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", _.FullName, _.GetAssembly().GetName().Name);
                });

                this._nameToTypeMapping.TryAdd(typeName, serializedType);
            }
        }

        class PlayDocument
        {
            public string id { get; set; }
            public string jsonString { get; set; }
        }

        class TestDocument
        {
            public KerberosTicketHashKey KerberosTicketHashKey { get; set; }
            public Dictionary<int, KerberosTicketHashKey> Dic1 { get; set; }
            public Dictionary<KerberosTicketHashKey, string> Dic2 { get; set; }

            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("pk")]
            public string Name { get; set; }

            public TestDocument(KerberosTicketHashKey kerberosTicketHashKey)
            {
                this.KerberosTicketHashKey = kerberosTicketHashKey;
                this.Dic1 = new Dictionary<int, KerberosTicketHashKey> { { 1, kerberosTicketHashKey } };
                this.Id = Guid.NewGuid().ToString();
                this.Name = this.Id;
            }

            private TestDocument()
            {
            }
        }

        readonly struct KerberosTicketHashKey : IEquatable<KerberosTicketHashKey>
        {
            private readonly int _hashCode;

            public readonly byte[] KerberosTicketHash;

            public KerberosTicketHashKey(byte[] kerberosTicketHash)
            {
                this.KerberosTicketHash = kerberosTicketHash;
                this._hashCode = ToInt32(kerberosTicketHash);
            }

            public override int GetHashCode()
            {
                return this._hashCode;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is KerberosTicketHashKey))
                {
                    return false;
                }
                return this.Equals((KerberosTicketHashKey)obj);
            }

            public bool Equals(KerberosTicketHashKey other)
            {
                return ByteArrayComparer.Comparer.Equals(this.KerberosTicketHash, other.KerberosTicketHash);
            }

            public static int ToInt32(byte[] bytes, uint offset = 0, bool isLittleEndian = false)
            {
                return ToInt32(
                    bytes[offset],
                    bytes[offset + 1],
                    bytes[offset + 2],
                    bytes[offset + 3],
                    isLittleEndian);
            }

            public static int ToInt32(
                byte firstByte,
                byte secondByte,
                byte thirdByte,
                byte fourthByte,
                bool isLittleEndian = false)
            {
                int firstShort = (int)ToUInt16(firstByte, secondByte, isLittleEndian);
                int secondShort = (int)ToUInt16(thirdByte, fourthByte, isLittleEndian);

                return isLittleEndian
                    ? (secondShort << 16) | firstShort
                    : (firstShort << 16) | secondShort;
            }

            public static ushort ToUInt16(
                byte firstByte,
                byte secondByte,
                bool isLittleEndian = false)
            {
                return (ushort)ToInt16(firstByte, secondByte, isLittleEndian);
            }

            public static short ToInt16(
                byte firstByte,
                byte secondByte,
                bool isLittleEndian = false)
            {
                return isLittleEndian
                    ? (short)((secondByte << 8) | firstByte)
                    : (short)((firstByte << 8) | secondByte);
            }
        }

        sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static readonly ByteArrayComparer Comparer = new ByteArrayComparer();

            private ByteArrayComparer()
            {
            }

            public bool Equals(byte[] firstByteArray, byte[] secondByteArray)
            {
                if (firstByteArray == secondByteArray)
                {
                    return true;
                }

                if (firstByteArray == null || secondByteArray == null)
                {
                    return false;
                }

                if (firstByteArray.Length != secondByteArray.Length)
                {
                    return false;
                }

                for (int i = 0; i < firstByteArray.Length; i++)
                {
                    if (firstByteArray[i] != secondByteArray[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(byte[] byteArray)
            {
                return byteArray.GetHashCode();
            }
        }

        class MyObject
        {
            public string id { get; set; }
            public string pk { get; set; }
            public int NumberField { get; set; }
            public bool IsTrue { get; set; }
            public Guid Guid { get; set; }
            public SerializedObject SerializedObject { get; set; }

            public MyObject(int i)
            {
                this.id = i.ToString();
                this.pk = "value";
                this.Guid = Guid.NewGuid();
                this.IsTrue = i < 5;
                this.NumberField = i;
                this.SerializedObject = new SerializedObject { Name = "Name: " + i };

            }

            public override string ToString() { return this.Guid + " - " + this.SerializedObject; }
        }

        class SerializedObject
        {
            public string Name { get; set; }

            public SerializedObject()
            {
            }

            public override string ToString() { return this.Name; }

            public static SerializedObject Parse(string name) { return new SerializedObject() { Name = name }; }
        }

        sealed class ObjectStringJsonConverter<TSource> : ObjectJsonConverter<TSource, string>
        {
            public ObjectStringJsonConverter(Func<string, TSource> deserializer)
                : base(
                    _ => _.ToString(),
                    deserializer)
            {
            }

            public ObjectStringJsonConverter(
                Func<TSource, string> serializer,
                Func<string, TSource> deserializer)
                : base(serializer, deserializer)
            {
            }

            public override void WriteJson(
                JsonWriter jsonWriter,
                object value,
                JsonSerializer jsonSerializer)
            {
                if (value == null)
                {
                    jsonWriter.WriteNull();
                }
                else
                {
                    jsonWriter.WriteValue(this.Serializer((TSource)value));
                }
            }

            public override object ReadJson(
                JsonReader jsonReader,
                Type objectType,
                object existingValue,
                JsonSerializer jsonSerializer)
            {
                return jsonReader.Value == null
                    ? default
                    : this.Deserializer((string)jsonReader.Value);
            }
        }
    }
}

