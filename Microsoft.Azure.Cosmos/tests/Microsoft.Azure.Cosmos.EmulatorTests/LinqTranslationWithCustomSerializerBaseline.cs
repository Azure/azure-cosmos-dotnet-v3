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
        private static CosmosClient CosmosClient;
        private static Database TestDb;
        private static Container TestContainer;

        private const int RecordCount = 100;
        private const int MaxValue = 500;
        private const int MaxStringLength = 100;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            CosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithCustomSerializer(new SystemTextJsonSerializer(new JsonSerializerOptions())));

            string dbName = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid():N}";
            TestDb = await CosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task Cleanup()
        {
            if (TestDb != null)
            {
                await TestDb.DeleteStreamAsync();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            TestContainer = await TestDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await TestContainer.DeleteContainerStreamAsync();
        }

        [TestMethod]
        public void TestMemberInitializerDotNet()
        {
            static DataObjectDotNet createDataObj(Random random)
            {
                DataObjectDotNet obj = new DataObjectDotNet
                {
                    NumericField = random.Next(MaxValue * 2) - MaxValue,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectDotNet>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, RecordCount, TestContainer, useCamelCaseSerializer);

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectDotNet() { NumericField = 12, StringField = "12" } : new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoft()
        {
            static DataObjectNewtonsoft createDataObj(Random random)
            {
                DataObjectNewtonsoft obj = new DataObjectNewtonsoft
                {
                    NumericField = random.Next(MaxValue * 2) - MaxValue,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoft>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, RecordCount, TestContainer, useCamelCaseSerializer);

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" } : new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }
            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerMultiSerializer()
        {
            static DataObjectMultiSerializer createDataObj(Random random)
            {
                DataObjectMultiSerializer obj = new DataObjectMultiSerializer
                {
                    NumericField = random.Next(MaxValue * 2) - MaxValue,
                    StringField = LinqTestsCommon.RandomString(random, random.Next(MaxStringLength)),
                    id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectMultiSerializer>> getQuery = LinqTestsCommon.GenerateTestCosmosData(createDataObj, RecordCount, TestContainer, useCamelCaseSerializer);

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" } : new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectMultiSerializer() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }
            this.ExecuteTestSuite(inputs);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input);
        }

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
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            [JsonPropertyName("id")]
            public string id { get; set; }

            [JsonPropertyName("Pk")]
            public string Pk { get; set; }
        }

        internal class DataObjectNewtonsoft : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            public string StringField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "id")]
            public string id { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "Pk")]
            public string Pk { get; set; }
        }

        internal class DataObjectMultiSerializer : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "id")]
            [JsonPropertyName("id")]
            public string id { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "Pk")]
            [JsonPropertyName("Pk")]
            public string Pk { get; set; }
        }
    }
}
