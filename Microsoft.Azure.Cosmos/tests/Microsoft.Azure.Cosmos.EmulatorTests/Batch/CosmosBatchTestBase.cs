//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
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

    public class CosmosBatchTestBase
    {
        protected static CosmosClient Client { get; set; }

        protected static CosmosClient GatewayClient { get; set; }

        protected static CosmosDatabase Database { get; set; }

        protected static CosmosDatabase SharedThroughputDatabase { get; set; }

        protected static CosmosDatabase GatewayDatabase { get; set; }

        protected static CosmosContainer JsonContainer { get; set; }

        protected static CosmosContainer GatewayJsonContainer { get; set; }

        protected static CosmosContainer LowThroughputJsonContainer { get; set; }

        protected static CosmosContainer GatewayLowThroughputJsonContainer { get; set; }

        protected static CosmosContainer SchematizedContainer { get; set; }

        protected static CosmosContainer GatewaySchematizedContainer { get; set; }

        protected static CosmosContainer SharedThroughputContainer { get; set; }

        internal static PartitionKeyDefinition PartitionKeyDefinition { get; set; }

        protected static Random Random { get; set; } = new Random();

        protected static LayoutResolverNamespace LayoutResolver { get; set; }

        protected static Layout TestDocLayout { get; set; }

        protected object PartitionKey1 { get; set; } = "TBD1";

        // Documents in PartitionKey1
        protected TestDoc TestDocPk1ExistingA { get; set; }

        // Documents in PartitionKey1
        protected TestDoc TestDocPk1ExistingB { get; set; }

        // Documents in PartitionKey1
        protected TestDoc TestDocPk1ExistingC { get; set; }

        // Documents in PartitionKey1
        protected TestDoc TestDocPk1ExistingD { get; set; }

        public static void ClassInit(TestContext context)
        {
            InitializeDirectContainers();
            InitializeGatewayContainers();
            InitializeSharedThroughputContainer();
        }

        private static void InitializeDirectContainers()
        {
            CosmosBatchTestBase.Client = TestCommon.CreateCosmosClient();
            CosmosBatchTestBase.Database = CosmosBatchTestBase.Client.CreateDatabaseAsync(Guid.NewGuid().ToString())
                .GetAwaiter().GetResult().Database;

            PartitionKeyDefinition partitionKeyDefinition = new PartitionKeyDefinition();
            partitionKeyDefinition.Paths.Add("/Status");

            CosmosBatchTestBase.LowThroughputJsonContainer = CosmosBatchTestBase.Database.CreateContainerAsync(
                new CosmosContainerSettings()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = partitionKeyDefinition
                },
                requestUnitsPerSecond: 400).GetAwaiter().GetResult().Container;

            CosmosBatchTestBase.PartitionKeyDefinition = ((CosmosContainerCore)CosmosBatchTestBase.LowThroughputJsonContainer).GetPartitionKeyDefinitionAsync(CancellationToken.None).GetAwaiter().GetResult();

            // Create a container with at least 2 physical partitions for effective cross-partition testing
            CosmosBatchTestBase.JsonContainer = CosmosBatchTestBase.Database.CreateContainerAsync(
                new CosmosContainerSettings()
                {
                    Id = Guid.NewGuid().ToString(),
                    PartitionKey = CosmosBatchTestBase.PartitionKeyDefinition
                },
                requestUnitsPerSecond: 12000).GetAwaiter().GetResult().Container;

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

            CosmosBatchTestBase.LayoutResolver = new LayoutResolverNamespace(testNamespace);
            CosmosBatchTestBase.TestDocLayout = CosmosBatchTestBase.LayoutResolver.Resolve(testSchema.SchemaId);

            BatchContainerSettings schematizedContainerSettings = new BatchContainerSettings()
            {
                Id = Guid.NewGuid().ToString(),
                PartitionKey = CosmosBatchTestBase.PartitionKeyDefinition,
                DefaultTimeToLive = (int)TimeSpan.FromDays(1).TotalSeconds // allow for TTL testing
            };

            SchemaPolicy schemaPolicy = new SchemaPolicy()
            {
                TableSchema = testNamespace,
            };

            schematizedContainerSettings.SchemaPolicy = schemaPolicy;

            CosmosBatchTestBase.SchematizedContainer = CosmosBatchTestBase.Database.CreateContainerAsync(
                schematizedContainerSettings,
                requestUnitsPerSecond: 12000).GetAwaiter().GetResult().Container;
        }

        private static void InitializeGatewayContainers()
        {
            CosmosBatchTestBase.GatewayClient = TestCommon.CreateCosmosClient(useGateway: true);
            CosmosBatchTestBase.GatewayDatabase = GatewayClient.GetDatabase(CosmosBatchTestBase.Database.Id);

            CosmosBatchTestBase.GatewayLowThroughputJsonContainer = CosmosBatchTestBase.GatewayDatabase.GetContainer(CosmosBatchTestBase.LowThroughputJsonContainer.Id);
            CosmosBatchTestBase.GatewayJsonContainer = CosmosBatchTestBase.GatewayDatabase.GetContainer(CosmosBatchTestBase.JsonContainer.Id);
            CosmosBatchTestBase.GatewaySchematizedContainer = CosmosBatchTestBase.GatewayDatabase.GetContainer(CosmosBatchTestBase.SchematizedContainer.Id);
        }

        private static void InitializeSharedThroughputContainer()
        {
            CosmosClient client = TestCommon.CreateCosmosClient();
            CosmosDatabase db = client.CreateDatabaseAsync(string.Format("Shared_{0}", Guid.NewGuid().ToString("N")), requestUnitsPerSecond: 20000).GetAwaiter().GetResult().Database;

            for (int index = 0; index < 5; index++)
            {
                ContainerResponse containerResponse = db.CreateContainerAsync(
                    new CosmosContainerSettings
                    {
                        Id = Guid.NewGuid().ToString(),
                        PartitionKey = CosmosBatchTestBase.PartitionKeyDefinition
                    })
                    .GetAwaiter().GetResult();

                Assert.AreEqual(true, bool.Parse(containerResponse.Headers.Get(WFConstants.BackendHeaders.ShareThroughput)));

                if (index == 2)
                {
                    CosmosBatchTestBase.SharedThroughputContainer = containerResponse.Container;
                }
            }

            CosmosBatchTestBase.SharedThroughputDatabase = db;
        }

        public static void ClassClean()
        {
            if (CosmosBatchTestBase.Client == null)
            {
                return;
            }

            if (CosmosBatchTestBase.Database != null)
            {
                CosmosBatchTestBase.Database.DeleteAsync().GetAwaiter().GetResult();
            }

            if (CosmosBatchTestBase.SharedThroughputDatabase != null)
            {
                CosmosBatchTestBase.SharedThroughputDatabase.DeleteAsync().GetAwaiter().GetResult();
            }

            CosmosBatchTestBase.Client.Dispose();
        }

        protected virtual async Task CreateJsonTestDocsAsync(CosmosContainer container)
        {
            this.TestDocPk1ExistingA = await CosmosBatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingB = await CosmosBatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingC = await CosmosBatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingD = await CosmosBatchTestBase.CreateJsonTestDocAsync(container, this.PartitionKey1);
        }

        protected virtual async Task CreateSchematizedTestDocsAsync(CosmosContainer container)
        {
            this.TestDocPk1ExistingA = await CosmosBatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingB = await CosmosBatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingC = await CosmosBatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
            this.TestDocPk1ExistingD = await CosmosBatchTestBase.CreateSchematizedTestDocAsync(container, this.PartitionKey1);
        }

        protected static TestDoc PopulateTestDoc(object partitionKey, int minDesiredSize = 20)
        {
            string description = new string('x', minDesiredSize);
            return new TestDoc()
            {
                Id = Guid.NewGuid().ToString(),
                Cost = CosmosBatchTestBase.Random.Next(),
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
                CosmosJsonSerializerCore serializer = new CosmosJsonSerializerCore();
                return serializer.ToStream<TestDoc>(testDoc);
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
                CosmosJsonSerializerCore serializer = new CosmosJsonSerializerCore();
                return serializer.FromStream<TestDoc>(stream);
            }
        }

        protected static async Task VerifyByReadAsync(CosmosContainer container, TestDoc doc, bool isStream = false, bool isSchematized = false, bool useEpk = false, string eTag = null)
        {
            Cosmos.PartitionKey partitionKey = CosmosBatchTestBase.GetPartitionKey(doc.Status, useEpk);

            if (isStream)
            {
                string id = CosmosBatchTestBase.GetId(doc, isSchematized);
                ItemRequestOptions requestOptions = CosmosBatchTestBase.GetItemRequestOptions(doc, isSchematized, useEpk, isPartOfBatch: false);
                CosmosResponseMessage response = await container.ReadItemStreamAsync(partitionKey, id, requestOptions);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(doc, CosmosBatchTestBase.StreamToTestDoc(response.Content, isSchematized));

                if (eTag != null)
                {
                    Assert.AreEqual(eTag, response.Headers.ETag);
                }
            }
            else
            {
                ItemResponse<TestDoc> response = await container.ReadItemAsync<TestDoc>(partitionKey, doc.Id);

                Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
                Assert.AreEqual(doc, response.Resource);

                if (eTag != null)
                {
                    Assert.AreEqual(eTag, response.Headers.ETag);
                }
            }
        }

        protected static async Task VerifyNotFoundAsync(CosmosContainer container, TestDoc doc, bool isSchematized = false, bool useEpk = false)
        {
            string id = CosmosBatchTestBase.GetId(doc, isSchematized);
            Cosmos.PartitionKey partitionKey = CosmosBatchTestBase.GetPartitionKey(doc.Status, useEpk);
            ItemRequestOptions requestOptions = CosmosBatchTestBase.GetItemRequestOptions(doc, isSchematized, useEpk, isPartOfBatch: false);

            CosmosResponseMessage response = await container.ReadItemStreamAsync(partitionKey, id, requestOptions);

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        }

        protected static RequestOptions GetUpdatedBatchRequestOptions(
            RequestOptions batchOptions = null,
            bool isSchematized = false,
            bool useEpk = false,
            object partitionKey = null)
        {
            if (isSchematized)
            {
                if (batchOptions == null)
                {
                    batchOptions = new RequestOptions();
                }

                if (batchOptions.Properties == null)
                {
                    batchOptions.Properties = new Dictionary<string, object>();
                }

                batchOptions.Properties.Add(WFConstants.BackendHeaders.BinaryPassthroughRequest, bool.TrueString);

                if (useEpk)
                {
                    string epk = new Microsoft.Azure.Documents.PartitionKey(partitionKey)
                                    .InternalKey
                                    .GetEffectivePartitionKeyString(CosmosBatchTestBase.PartitionKeyDefinition);

                    batchOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);
                    batchOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKey, CosmosBatchTestBase.HexStringToBytes(epk));
                }
            }

            return batchOptions;
        }

        protected static Cosmos.PartitionKey GetPartitionKey(object partitionKey, bool useEpk = false)
        {
            return useEpk ? null : new Cosmos.PartitionKey(partitionKey);
        }

        protected static string GetId(TestDoc doc, bool isSchematized)
        {
            if (isSchematized)
            {
                return "cdbBinaryIdRequest";
            }

            return doc.Id;
        }


        protected static ItemRequestOptions GetItemRequestOptions(TestDoc doc, bool isSchematized, bool useEpk = false, bool isPartOfBatch = true, int? ttlInSeconds = null)
        {
            ItemRequestOptions requestOptions = null;
            if (isSchematized)
            {
                requestOptions = new ItemRequestOptions()
                {
                    Properties = new Dictionary<string, object>
                    {
                        { WFConstants.BackendHeaders.BinaryId, Encoding.UTF8.GetBytes(doc.Id) }
                    }
                };

                if (ttlInSeconds.HasValue)
                {
                    requestOptions.Properties.Add(WFConstants.BackendHeaders.TimeToLiveInSeconds, ttlInSeconds.Value.ToString());
                }

                if (useEpk)
                {
                    string epk = new Microsoft.Azure.Documents.PartitionKey(doc.Status)
                                    .InternalKey
                                    .GetEffectivePartitionKeyString(CosmosBatchTestBase.PartitionKeyDefinition);

                    requestOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKeyString, epk);
                    requestOptions.Properties.Add(WFConstants.BackendHeaders.EffectivePartitionKey, CosmosBatchTestBase.HexStringToBytes(epk));
                }

                if (!isPartOfBatch)
                {
                    requestOptions.Properties.Add(WFConstants.BackendHeaders.BinaryPassthroughRequest, bool.TrueString);
                }
            }

            return requestOptions;
        }

        protected static async Task<TestDoc> CreateJsonTestDocAsync(CosmosContainer container, object partitionKey, int minDesiredSize = 20)
        {
            TestDoc doc = CosmosBatchTestBase.PopulateTestDoc(partitionKey, minDesiredSize);
            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(doc, CosmosBatchTestBase.GetPartitionKey(partitionKey));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            return doc;
        }

        protected static async Task<TestDoc> CreateSchematizedTestDocAsync(CosmosContainer container, object partitionKey, int? ttlInSeconds = null)
        {
            TestDoc doc = CosmosBatchTestBase.PopulateTestDoc(partitionKey);
            CosmosResponseMessage createResponse = await container.CreateItemStreamAsync(
                CosmosBatchTestBase.GetPartitionKey(partitionKey),
                doc.ToHybridRowStream(),
                CosmosBatchTestBase.GetItemRequestOptions(doc, isSchematized: true, isPartOfBatch: false, ttlInSeconds: ttlInSeconds));
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
                Assert.IsTrue(row.ReadFrom(stream, (int)length, HybridRowVersion.V1, CosmosBatchTestBase.LayoutResolver));
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
                row.InitLayout(HybridRowVersion.V1, CosmosBatchTestBase.TestDocLayout, CosmosBatchTestBase.LayoutResolver);
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

        private class BatchContainerSettings : CosmosContainerSettings
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
