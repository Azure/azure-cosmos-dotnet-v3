//-----------------------------------------------------------------------
// <copyright file="LinqTranslationWithCustomSerializerBaseline.cs" company="Microsoft Corporation">
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
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BaselineTest;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [SDK.EmulatorTests.TestClass]
    public class LinqTranslationWithCustomSerializerBaseline : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithCustomSerializer(new SystemTextJsonSerializer(new JsonSerializerOptions())).WithConnectionModeGateway());

            string dbName = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid():N}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task Cleanup()
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
        public async Task TestCleanup()
        {
            await testContainer.DeleteContainerStreamAsync();
        }

        //todo mayapainter: is this the right location for these tests?

        [TestMethod]
        public void TestMemberInitializerDotNet()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            const int MaxStringLength = 100;

            static DataObjectDotNet createDataObj(Random random)
            {
                DataObjectDotNet obj = new DataObjectDotNet
                {
                    NumericField = random.Next(NumAbsMax * 2) - NumAbsMax,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }

            //mayapainter - question, is camelcase honored upon creation and/or querying
            Func<bool, IQueryable<DataObjectDotNet>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ DataObject initializer with constant value", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 12, StringField = "12" })),
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 12, StringField = "12" })),
                new LinqTestInput("Deeper than top level reference", b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectDotNet() { NumericField = 12, StringField = "12" } : new DataObjectDotNet() { NumericField = 12, StringField = "12" })),

                // Negative test case: serializing only field name using custom serializer not currently supported
                new LinqTestInput("Filter w/ DataObject initializer with member initialization", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"))
            };
            this.ExecuteTestSuite(inputs);
        }

        // todo mayapainter: isolate reused parts, pass type of obj?

        [TestMethod]
        public void TestMemberInitializerNewtonsoft()
        {
            const int Records = 100;
            const int NumAbsMax = 500;
            const int MaxStringLength = 100;

            static DataObjectNewtonsoft createDataObj(Random random)
            {
                DataObjectNewtonsoft obj = new DataObjectNewtonsoft
                {
                    NumericField = random.Next(NumAbsMax * 2) - NumAbsMax,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    Id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }
            Func<bool, IQueryable<DataObjectNewtonsoft>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, Records, testContainer);

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ DataObject initializer with constant value", b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" })),
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" })),
                new LinqTestInput("Deeper than top level reference", b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" } : new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" })),

                // Negative test case: serializing only field name using custom serializer not currently supported
                new LinqTestInput("Filter w/ DataObject initializer with member initialization", b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"))
            };
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            // mayapainter: add serializer options to linq test input
            return LinqTestsCommon.ExecuteTest(input);
        }

        // Custom serializer that uses System.Text.Json.JsonSerializer
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

        internal class DataObjectDotNet : LinqTestObject
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

        internal class DataObjectNewtonsoft : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "number_newtonsoft")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "String_value_newtonsoft")]
            public string StringField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "id_newtonsoft")]
            public string Id { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "Pk_newtonsoft")]
            public string Pk { get; set; }
        }

        internal class DataObjectMultiSerializer : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "number_newtonsoft")]
            [JsonPropertyName("number")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "String_value_newtonsoft")]
            [JsonPropertyName("String_value")]
            public string StringField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "id_newtonsoft")]
            [JsonPropertyName("id")]
            public string Id { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "Pk_newtonsoft")]
            [JsonPropertyName("Pk")]
            public string Pk { get; set; }
        }
    }
}
