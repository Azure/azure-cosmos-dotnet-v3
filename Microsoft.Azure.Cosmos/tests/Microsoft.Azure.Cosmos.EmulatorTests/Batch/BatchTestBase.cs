//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.IO;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Layouts;
    using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    public class BatchTestBase
    {
        protected static CosmosClient Client { get; set; }

        protected static CosmosClient GatewayClient { get; set; }

        protected static Cosmos.Database Database { get; set; }

        protected static Cosmos.Database SharedThroughputDatabase { get; set; }

        protected static Cosmos.Database GatewayDatabase { get; set; }

        protected static Container JsonContainer { get; set; }

        protected static Container GatewayJsonContainer { get; set; }

        protected static Container LowThroughputJsonContainer { get; set; }

        protected static Container GatewayLowThroughputJsonContainer { get; set; }

        protected static Container SchematizedContainer { get; set; }

        protected static Container GatewaySchematizedContainer { get; set; }

        protected static Container SharedThroughputContainer { get; set; }

        internal static PartitionKeyDefinition PartitionKeyDefinition { get; set; }

        protected static Random Random { get; set; } = new Random();

        protected static LayoutResolverNamespace LayoutResolver { get; set; }

        protected static Layout TestDocLayout { get; set; }

        protected object PartitionKey1 { get; set; } = "TBD1";

        // Documents in PartitionKey1
        protected TestDoc TestDocPk1ExistingA { get; set; }
        protected TestDoc TestDocPk1ExistingB { get; set; }
        protected TestDoc TestDocPk1ExistingC { get; set; }
        protected TestDoc TestDocPk1ExistingD { get; set; }
        protected TestDoc TestDocPk1ExistingE { get; set; }

        public static void ClassInit(TestContext context)
        {
            InitializeDirectContainers();
            InitializeGatewayContainers();
            InitializeSharedThroughputContainer();
        }

        private static void InitializeDirectContainers()
        {

            BatchTestBase.Client = TestCommon.CreateCosmosClient(builder => builder.WithConsistencyLevel(Cosmos.ConsistencyLevel.Session));
            BatchTestBase.Database = BatchTestBase.Client.CreateDatabaseAsync(Guid.NewGuid().ToString())
                .GetAwaiter().GetResult().Database;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/Status");

            BatchTestBase.LowThroughputJsonContainer = BatchTestBase.Database.CreateContainerAsync(
                new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                },
                throughput: 400).GetAwaiter().GetResult().Container;

            BatchTestBase.PartitionKeyDefinition = ((ContainerInternal)(ContainerInlineCore)BatchTestBase.LowThroughputJsonContainer).GetPartitionKeyDefinitionAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Create a container with at least 2 physical partitions for effective cross-partition testing
            BatchTestBase.JsonContainer = BatchTestBase.Database.CreateContainerAsync(
                new ContainerProperties()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = BatchTestBase.PartitionKeyDefinition
                },
                throughput: 12000).GetAwaiter().GetResult().Container;

            Serialization.HybridRow.Schemas.Schema testSchema = TestDoc.GetSchema();
            Namespace testNamespace = new Namespace()
            {
                Name = "Test",
                Version = SchemaLanguageVersion.V1,
                Schemas = new List<Serialization.HybridRow.Schemas.Schema>()
                {
                    testSchema
                }
            };

            BatchTestBase.LayoutResolver = new LayoutResolverNamespace(testNamespace);
            BatchTestBase.TestDocLayout = BatchTestBase.LayoutResolver.Resolve(testSchema.SchemaId);

            BatchContainerProperties schematizedContainerProperties = new BatchContainerProperties()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = BatchTestBase.PartitionKeyDefinition,
                DefaultTimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds // allow for TTL testing
            };

            SchemaPolicy schemaPolicy = new SchemaPolicy()
            {
                TableSchema = testNamespace,
            };

            schematizedContainerProperties.SchemaPolicy = schemaPolicy;

            BatchTestBase.SchematizedContainer = BatchTestBase.Database.CreateContainerAsync(
                schematizedContainerProperties,
                throughput: 12000).GetAwaiter().GetResult().Container;
        }

        private static void InitializeGatewayContainers()
        {
            BatchTestBase.GatewayClient = TestCommon.CreateCosmosClient(useGateway: true);
            BatchTestBase.GatewayDatabase = GatewayClient.GetDatabase(BatchTestBase.Database.Id);

            BatchTestBase.GatewayLowThroughputJsonContainer = BatchTestBase.GatewayDatabase.GetContainer(BatchTestBase.LowThroughputJsonContainer.Id);
            BatchTestBase.GatewayJsonContainer = BatchTestBase.GatewayDatabase.GetContainer(BatchTestBase.JsonContainer.Id);
            BatchTestBase.GatewaySchematizedContainer = BatchTestBase.GatewayDatabase.GetContainer(BatchTestBase.SchematizedContainer.Id);
        }

        private static void InitializeSharedThroughputContainer()
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
            Cosmos.Database db = client.CreateDatabaseAsync(string.Format("Shared_{0}", Guid.NewGuid().ToString("N")), throughput: 20000).GetAwaiter().GetResult().Database;

            for (int index = 0; index < 5; index++)
            {
                ContainerResponse containerResponse = db.CreateContainerAsync(
                    new ContainerProperties
                    {
                        Id = Guid.NewGuid().ToString(),
                        PartitionKey = BatchTestBase.PartitionKeyDefinition
                    })
                    .GetAwaiter().GetResult();

                Assert.AreEqual(true, bool.Parse(containerResponse.Headers.Get(WFConstants.BackendHeaders.ShareThroughput)));

                if (index == 2)
                {
                    BatchTestBase.SharedThroughputContainer = containerResponse.Container;
                }
            }

            BatchTestBase.SharedThroughputDatabase = db;
        }

        public static void ClassClean()
        {
            if (BatchTestBase.Client == null)
            {
                return;
            }

            if (BatchTestBase.Database != null)
            {
                BatchTestBase.Database.DeleteStreamAsync().GetAwaiter().GetResult();
            }

            if (BatchTestBase.SharedThroughputDatabase != null)
            {
                BatchTestBase.SharedThroughputDatabase.DeleteStreamAsync().GetAwaiter().GetResult();
            }

            BatchTestBase.Client.Dispose();
        }

        protected virtual async Task CreateJsonTestDocsAsync(Container container)
        {
            this.TestDocPk1ExistingA = await BatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingB = await BatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingC = await BatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingD = await BatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
        }

        protected virtual async Task CreateSchematizedTestDocsAsync(Container container)
        {
            this.TestDocPk1ExistingA = await BatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingB = await BatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingC = await BatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingD = await BatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
        }

        protected static TestDoc PopulateTestDoc(object partitionKey, int minDesiredSize = 20)
        {
            string description = new string('x', minDesiredSize);
            return new TestDoc()
            {
                Id = Guid.NewGuid().ToString(),
                Cost = BatchTestBase.Random.Next(),
                Description = description,
                Status = partitionKey.ToString()
            };
        }

        protected TestDoc GetTestDocCopy(TestDoc testDoc)
        {
            return new TestDoc()
            {
                Id = testDoc.Id,
                Cost = testDoc.Cost,
                Description = testDoc.Description,
                Status = testDoc.Status
            };
        }

        protected static Stream TestDocToStream(TestDoc testDoc, bool isSchematized)
        {
            if (isSchematized)
            {
                return testDoc.ToHybridRowStream();
            }
            else
            {
                
                return TestCommon.SerializerCore.ToStream<TestDoc>(testDoc);
            }
        }

        protected static TestDoc StreamToTestDoc(Stream stream, bool isSchematized)
        {
            if (isSchematized)
            {
                return TestDoc.FromHybridRowStream(stream);
            }
            else
            {
                return TestCommon.SerializerCore.FromStream<TestDoc>(stream);
            }
        }

        protected static async Task VerifyByReadAsync(Container container, TestDoc doc, bool isStream = false, bool isSchematized = false, bool useEpk = false, string eTag = null)
        {
            Cosmos.PartitionKey partitionKey = BatchTestBase.GetPartitionKey(doc.Status);

            if (isStream)
            {
                string id = BatchTestBase.GetId(doc, isSchematized);
                ItemRequestOptions requestOptions = BatchTestBase.GetItemRequestOptions(doc, isSchematized, useEpk);
                ResponseMessage response = await container.ReadItemStreamAsync(id, partitionKey, requestOptions);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(doc, BatchTestBase.StreamToTestDoc(response.Content, isSchematized));

                if (eTag != null)
                {
                    Assert.AreEqual(eTag, response.Headers.ETag);
                }
            }
            else
            {
                ItemResponse<TestDoc> response = await container.ReadItemAsync<TestDoc>(doc.Id, partitionKey);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(doc, response.Resource);

                if (eTag != null)
                {
                    Assert.AreEqual(eTag, response.Headers.ETag);
                }
            }
        }

        protected static async Task VerifyNotFoundAsync(Container container, TestDoc doc, bool isSchematized = false, bool useEpk = false)
        {
            string id = BatchTestBase.GetId(doc, isSchematized);
            Cosmos.PartitionKey partitionKey = BatchTestBase.GetPartitionKey(doc.Status);
            ItemRequestOptions requestOptions = BatchTestBase.GetItemRequestOptions(doc, isSchematized, useEpk);

            ResponseMessage response = await container.ReadItemStreamAsync(id, partitionKey, requestOptions);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        protected static TransactionalBatchRequestOptions GetUpdatedBatchRequestOptions(
            TransactionalBatchRequestOptions batchOptions = null,
            bool isSchematized = false,
            bool useEpk = false,
            object partitionKey = null)
        {
            if (isSchematized)
            {
                if (batchOptions == null)
                {
                    batchOptions = new TransactionalBatchRequestOptions();
                }

                Dictionary<string, object> properties = new Dictionary<string, object>()
                {
                    { WFConstants.BackendHeaders.BinaryPassthroughRequest, bool.TrueString }
                };

                if (batchOptions.Properties != null)
                {
                    foreach(KeyValuePair<string, object> entry in batchOptions.Properties)
                    {
                        properties.Add(entry.Key, entry.Value);
                    }
                }

                if (useEpk)
                {
                    string epk = new Microsoft.Azure.Documents.PartitionKey(partitionKey)
                                    .InternalKey
                                    .GetEffectivePartitionKeyString(BatchTestBase.PartitionKeyDefinition);

                    properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);
                    properties.Add(WFConstants.BackendHeaders.EffectivePartitionKey, BatchTestBase.HexStringToBytes(epk));
                    batchOptions.IsEffectivePartitionKeyRouting = true;
                }

                batchOptions.Properties = properties;
            }

            return batchOptions;
        }

        protected static Cosmos.PartitionKey GetPartitionKey(object partitionKey)
        {
            return new Cosmos.PartitionKey(partitionKey);
        }

        protected static string GetId(TestDoc doc, bool isSchematized)
        {
            if (isSchematized)
            {
                return "cdbBinaryIdRequest";
            }

            return doc.Id;
        }

        internal static TransactionalBatchItemRequestOptions GetBatchItemRequestOptions(TestDoc doc, bool isSchematized, bool useEpk = false, int? ttlInSeconds = null)
        {
            TransactionalBatchItemRequestOptions requestOptions = new TransactionalBatchItemRequestOptions();
            if (PopulateRequestOptions(requestOptions, doc, isSchematized, useEpk, ttlInSeconds))
            {
                return requestOptions;
            }

            return null;
        }

        protected static ItemRequestOptions GetItemRequestOptions(TestDoc doc, bool isSchematized, bool useEpk = false, int? ttlInSeconds = null)
        {
            ItemRequestOptions requestOptions = new ItemRequestOptions();
            bool wasPopulated = BatchTestBase.PopulateRequestOptions(requestOptions, doc, isSchematized, useEpk, ttlInSeconds);
            if (isSchematized)
            {
                Dictionary<string, object> properties = requestOptions.Properties as Dictionary<string, object>;
                properties.Add(WFConstants.BackendHeaders.BinaryPassthroughRequest, bool.TrueString);
                wasPopulated = true;
            }

            if (wasPopulated)
            {
                return requestOptions;
            }

            return null;
        }

        protected static async Task<TestDoc> CreateJsonTestDocAsync(Container container, object partitionKey, int minDesiredSize = 20)
        {
            TestDoc doc = BatchTestBase.PopulateTestDoc(partitionKey, minDesiredSize);
            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(doc, BatchTestBase.GetPartitionKey(partitionKey));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            return doc;
        }

        protected static async Task<TestDoc> CreateSchematizedTestDocAsync(Container container, object partitionKey, int? ttlInSeconds = null)
        {
            TestDoc doc = BatchTestBase.PopulateTestDoc(partitionKey);
            ResponseMessage createResponse = await container.CreateItemStreamAsync(
                doc.ToHybridRowStream(),
                BatchTestBase.GetPartitionKey(partitionKey),
                BatchTestBase.GetItemRequestOptions(doc, isSchematized: true, ttlInSeconds: ttlInSeconds));
            Assert.AreEqual(
                HttpStatusCode.Created,
                createResponse.StatusCode);
            return doc;
        }

        protected static byte[] HexStringToBytes(string input)
        {
            byte[] bytes = new byte[input.Length / 2];
            for (int i = 0; i < input.Length; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(input.Substring(i, 2), 16);
            }

            return bytes;
        }

        internal static ISessionToken GetSessionToken(string sessionToken)
        {
            string[] tokenParts = sessionToken.Split(':');
            return SessionTokenHelper.Parse(tokenParts[1]);
        }

        internal static string GetDifferentLSNToken(string token, long lsnIncrement)
        {
            string[] tokenParts = token.Split(':');
            ISessionToken sessionToken = SessionTokenHelper.Parse(tokenParts[1]);
            ISessionToken differentSessionToken = BatchTestBase.CreateSessionToken(sessionToken, sessionToken.LSN + lsnIncrement);
            return string.Format(CultureInfo.InvariantCulture, "{0}:{1}", tokenParts[0], differentSessionToken.ConvertToString());
        }

        internal static ISessionToken CreateSessionToken(ISessionToken from, long globalLSN)
        {
            // Creates session token with specified GlobalLSN
            if (from is SimpleSessionToken)
            {
                return new SimpleSessionToken(globalLSN);
            }
            else if (from is VectorSessionToken)
            {
                return new VectorSessionToken(from as VectorSessionToken, globalLSN);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private static bool PopulateRequestOptions(RequestOptions requestOptions, TestDoc doc, bool isSchematized, bool useEpk, int? ttlInSeconds)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>();
            requestOptions.Properties = properties;
            if (isSchematized)
            {
                properties.Add(WFConstants.BackendHeaders.BinaryId, Encoding.UTF8.GetBytes(doc.Id));

                if (ttlInSeconds.HasValue)
                {
                    properties.Add(WFConstants.BackendHeaders.TimeToLiveInSeconds, ttlInSeconds.Value.ToString());
                }

                if (useEpk)
                {
                    string epk = new Microsoft.Azure.Documents.PartitionKey(doc.Status)
                                    .InternalKey
                                    .GetEffectivePartitionKeyString(BatchTestBase.PartitionKeyDefinition);

                    properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);
                    properties.Add(WFConstants.BackendHeaders.EffectivePartitionKey, BatchTestBase.HexStringToBytes(epk));
                    requestOptions.IsEffectivePartitionKeyRouting = true;
                }

                return true;
            }

            return false;
        }

#pragma warning disable CA1034
        public class TestDoc
#pragma warning restore CA1034
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            public int Cost { get; set; }

            public string Description { get; set; }

            public string Status { get; set; }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.Cost == doc.Cost
                       && this.Description == doc.Description
                       && this.Status == doc.Status;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + this.Cost.GetHashCode();
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Description);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Status);
                return hashCode;
            }

            public override string ToString()
            {
                return JsonConvert.SerializeObject(this);
            }

            public static TestDoc FromHybridRowStream(Stream stream)
            {
                uint length = 0;
                using (BinaryReader binaryReader = new BinaryReader(stream, Encoding.Default, leaveOpen: true))
                {
                    TestDoc.SkipBinaryField(binaryReader); // binaryId
                    TestDoc.SkipBinaryField(binaryReader); // EPK

                    binaryReader.ReadByte();
                    length = binaryReader.ReadUInt32();
                }

                RowBuffer row = new RowBuffer((int)length);
                Assert.IsTrue(row.ReadFrom(stream, (int)length, HybridRowVersion.V1, BatchTestBase.LayoutResolver));
                RowReader reader = new RowReader(ref row);

                TestDoc testDoc = new TestDoc();
                while (reader.Read())
                {
                    Result r;
                    switch (reader.Path)
                    {
                        case "Id":
                            r = reader.ReadString(out string id);
                            Assert.AreEqual(Result.Success, r);
                            testDoc.Id = id;
                            break;

                        case "Cost":
                            r = reader.ReadInt32(out int cost);
                            Assert.AreEqual(Result.Success, r);
                            testDoc.Cost = cost;
                            break;

                        case "Status":
                            r = reader.ReadString(out string status);
                            Assert.AreEqual(Result.Success, r);
                            testDoc.Status = status;
                            break;

                        case "Description":
                            r = reader.ReadString(out string description);
                            Assert.AreEqual(Result.Success, r);
                            testDoc.Description = description;
                            break;
                    }
                }

                return testDoc;
            }

            public MemoryStream ToHybridRowStream()
            {
                RowBuffer row = new RowBuffer(80000);
                row.InitLayout(HybridRowVersion.V1, BatchTestBase.TestDocLayout, BatchTestBase.LayoutResolver);
                Result r = RowWriter.WriteBuffer(ref row, this, TestDoc.WriteDoc);
                Assert.AreEqual(Result.Success, r);
                MemoryStream output = new MemoryStream(row.Length);
                row.WriteTo(output);
                output.Position = 0;
                return output;
            }

            public static Serialization.HybridRow.Schemas.Schema GetSchema()
            {
                return new Serialization.HybridRow.Schemas.Schema()
                {
                    SchemaId = new SchemaId(-1),
                    Name = "TestDoc",
                    Type = TypeKind.Schema,
                    Properties = new List<Property>()
                    {
                        new Property()
                        {
                            Path = "Id",
                            PropertyType = new PrimitivePropertyType()
                            {
                                Type = TypeKind.Utf8,
                                Storage = StorageKind.Variable
                            }
                        },
                        new Property()
                        {
                            Path = "Cost",
                            PropertyType = new PrimitivePropertyType()
                            {
                                Type = TypeKind.Int32,
                                Storage = StorageKind.Fixed
                            }
                        },
                        new Property()
                        {
                            Path = "Status",
                            PropertyType = new PrimitivePropertyType()
                            {
                                Type = TypeKind.Utf8,
                                Storage = StorageKind.Variable
                            }
                        },
                        new Property()
                        {
                            Path = "Description",
                            PropertyType = new PrimitivePropertyType()
                            {
                                Type = TypeKind.Utf8,
                                Storage = StorageKind.Variable
                            }
                        }
                    },
                    PartitionKeys = new List<Serialization.HybridRow.Schemas.PartitionKey>()
                    {
                        new Serialization.HybridRow.Schemas.PartitionKey()
                        {
                            Path = "Status"
                        }
                    },
                    Options = new SchemaOptions()
                    {
                        DisallowUnschematized = true
                    }
                };
            }

            private static Result WriteDoc(ref RowWriter writer, TypeArgument typeArg, TestDoc doc)
            {
                Result r = writer.WriteString("Id", doc.Id);
                if (r != Result.Success)
                {
                    return r;
                }

                r = writer.WriteInt32("Cost", doc.Cost);
                if (r != Result.Success)
                {
                    return r;
                }

                r = writer.WriteString("Status", doc.Status);
                if (r != Result.Success)
                {
                    return r;
                }

                r = writer.WriteString("Description", doc.Description);
                if (r != Result.Success)
                {
                    return r;
                }

                return Result.Success;
            }

            private static void SkipBinaryField(BinaryReader binaryReader)
            {
                binaryReader.ReadByte();
                uint length = binaryReader.ReadUInt32();
                binaryReader.ReadBytes((int)length);
            }
        }

        private class BatchContainerProperties : ContainerProperties
        {
            [JsonProperty("schemaPolicy")]
            public SchemaPolicy SchemaPolicy { get; set; }
        }

        private class SchemaPolicy
        {
            public Namespace TableSchema { get; set; }

            public Namespace TypeSchema { get; set; }
        }
    }
}
