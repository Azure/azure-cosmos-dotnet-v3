//-----------------------------------------------------------------------
// <copyright file="LinqCustomSerializerBaseline.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Linq.Dynamic;
    using System.Text.Json;
    using System.Threading.Tasks;
    using BaselineTest;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqCustomSerializerBaselineTests : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient(useCustomSeralizer: true);
            string dbName = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerStreamAsync();
        }

        // Custom serializer that uses System.Text.Json.JsonSerializer instead of NewtonsoftJson.JsonSerializer
        private class SystemTextJsonSerializer : CosmosSerializer
        {
            private readonly JsonObjectSerializer systemTextJsonSerializer;

            public SystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions)
            {
                this.systemTextJsonSerializer = new JsonObjectSerializer(jsonSerializerOptions);
            }

            public override T FromStream<T>(Stream stream)
            {
                if (stream == null)
                    throw new ArgumentNullException(nameof(stream));

                using (stream)
                {
                    if (stream.CanSeek && stream.Length == 0)
                    {
                        return default;
                    }

                    if (typeof(Stream).IsAssignableFrom(typeof(T)))
                    {
                        return (T)(object)stream;
                    }

                    return (T)this.systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                this.systemTextJsonSerializer.Serialize(streamPayload, input, typeof(T), default);
                streamPayload.Position = 0;
                return streamPayload;
            }
        }

        internal class DataObject : LinqTestObject
        {
            [System.Text.Json.Serialization.JsonPropertyName("int_value")]
            public int Int { get; set; }

            [Newtonsoft.Json.JsonProperty("string_value", NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
            public string String { get; set; }

            public bool Bool { get; set; }

            [Newtonsoft.Json.JsonProperty("id")]
            public string Id { get; set; }

            public string Pk { get; set; }

            [Newtonsoft.Json.JsonExtensionData(ReadData = true, WriteData = true)]
            public Dictionary<string, object> NewtonsoftExtensionData { get; set; }

            [System.Text.Json.Serialization.JsonExtensionData()]
            public Dictionary<string, object> NetExtensionData { get; set; }

            protected override string SerializeForTestBaseline()
            {
                DataObject copy = FilterSystemProperties(this);
                return JsonConvert.SerializeObject(copy);
            }

            private static DataObject FilterSystemProperties(DataObject data)
            {
                // When DataObject is returned by cosmos db, it contains system properties. These system properties are not present for offline execution.
                // Additionally the system properties make it in the NewtonsoftExtensionData, due to deserialization logic based on JsonExtensionData attribute.
                HashSet<string> SystemProperties = new HashSet<string> { "_rid", "_self", "_etag", "_attachments", "_ts" };
                Dictionary<string, object> newtonsoftExtensionData = new Dictionary<string, object>(data.NewtonsoftExtensionData
                            .Where(kvp => !SystemProperties.Contains(kvp.Key)));
                return new DataObject
                {
                    Bool = data.Bool,
                    Id = data.Id,
                    Int = data.Int,
                    NetExtensionData = data.NetExtensionData,
                    NewtonsoftExtensionData = newtonsoftExtensionData,
                    Pk = data.Pk,
                    String = data.String
                };
            }
        }

        [TestMethod]
        public void TestQueryUse()
        {
            const int Records = 5;
            const int NumAbsMax = 500;
            int i = 0;
            DataObject createDataObj(Random random)
            {
                // Dictionaries marked as Newtonsoft.JsonExtensionData are not stored as a collection in the document, but rather get flattened as part of serialization.
                // Dictionaries marked as .net's JsonExtensiondata are stored as is, since newtonsoft (used by .NET sdk) ignores this attribute.
                // Here's a sample document for DataObject:
                // {
                //     "Int": 0,
                //     "string_value": "0",
                //     "Bool": true,
                //     "id": "0",
                //     "Pk": "Test",
                //     "NetExtensionData": {
                //         "NET1": 0,
                //         "NET2": "0"
                //     },
                //     "Newtonsoft1": 0,
                //     "Newtonsoft2": "0",
                // }
                DataObject obj = new DataObject
                {
                    Int = i % NumAbsMax,
                    String = i % 2 == 0 ? i.ToString() : null,
                    Bool = i % 2 == 0,
                    Id = i.ToString(),
                    Pk = "Test",
                    NewtonsoftExtensionData = new Dictionary<string, object>
                    {
                        { "Newtonsoft1", i },
                        { "Newtonsoft2", i.ToString() }
                    },
                    NetExtensionData = new Dictionary<string, object>
                    {
                        { "NET1", i },
                        { "NET2", i.ToString() }
                    }
                };

                i++;

                return obj;
            }
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter on bool", b => getQuery(b).Where(doc => doc.Bool)),
                new LinqTestInput("Filter on int", b => getQuery(b).Where(doc => doc.Int % 3 == 0)),
                new LinqTestInput("Filter on string", b => getQuery(b).Where(doc => doc.String == null ? false : doc.String.Contains("0"))),

                // Query produced in following case is :
                // SELECT VALUE root["NewtonsoftExtensionData"] FROM root
                // Since underlying documents don't really contain a property "NewtonsoftExtensiondata", query returns empty results.
                // This also causes a mismatch between offline evaluation and actual query evaluation, so we skip the verfication.
                new LinqTestInput("Select newtonsoft extension data", b => getQuery(b).Select(doc => doc.NewtonsoftExtensionData), skipVerification: true),

                // Since .NET extension property is "not supported" (not flattened during serialization)
                //   NetExtensionData property on the object roundtrips successfully across both document persistence and query.
                new LinqTestInput("Select .NET extension data", b => getQuery(b).Select(doc => doc.NetExtensionData))

                // ISSUE-TODO-adityasa-2023/4/28 - test by adding documents that don't adhere to the shape of the C# object.
                // This requires an ability to externally add a document to the collection and also add it to the offline data (when verification is desired).
            };

            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input, includeResults: true);
        }
    }
}
