//-----------------------------------------------------------------------
// <copyright file="LinqTranslationWithCustomSerializerBaseline.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using BaselineTest;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Dynamic;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using System.Threading.Tasks;
    using global::Azure.Core.Serialization;
    using System.IO;
    using System.Text.Json.Serialization;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqTranslationWithCustomSerializerBaseline : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            Uri uri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                EnableEndpointDiscovery = true,
            };

            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) 
                => cosmosClientBuilder.WithCustomSerializer(new SystemTextJsonSerializer(new JsonSerializerOptions())).WithConnectionModeGateway());

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
            [JsonPropertyName("number")]
            public double NumericField { get; set; }

            [JsonPropertyName("String_value")]
            public string StringField { get; set; }

            [JsonPropertyName("id")]
            public string Id { get; set; }

            [JsonPropertyName("Pk")]
            public string Pk { get; set; }
        }

        [TestMethod]
        public void TestMemberInitializer()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            const int MaxStringLength = 100;
            DataObject createDataObj(Random random)
            {
                DataObject obj = new DataObject
                {
                    NumericField = random.Next(NumAbsMax * 2) - NumAbsMax,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }
            Func<bool, IQueryable<DataObject>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ DataObject initializer with constant value", b => getQuery(b).Where(doc => doc == new DataObject() { NumericField = 12, StringField = "12" })),
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObject() { NumericField = 12, StringField = "12" }))

                // Negative test case: serializing only field name using custom serializer not currently supported
                //new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField })),
                //new LinqTestInput("Filter w/ DataObject initializer with member initialization", b => getQuery(b).Where(doc => doc == new DataObject() { NumericField = doc.NumericField, StringField = doc.StringField }))
            };
            this.ExecuteTestSuite(inputs);
        }


        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }
    }
}