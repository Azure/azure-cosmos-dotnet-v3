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
    using System.Reflection;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using System.Xml.Schema;
    using BaselineTest;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [SDK.EmulatorTests.TestClass]
    public class LinqTranslationWithCustomSerializerBaseline : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient CosmosClient;
        private static Database TestDb;
        private static Container TestContainer;

        private const int RecordCount = 3;
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
            Func<bool, IQueryable<DataObjectDotNet>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectDotNet>> getQueryDefault;
            string insertedData;
            (getQueryCamelCase, getQueryDefault, insertedData) = this.InsertDataAndGetQueryables<DataObjectDotNet>();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectDotNet>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectDotNet() { NumericField = 12, StringField = "12" } : new DataObjectDotNet() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoft()
        {
            Func<bool, IQueryable<DataObjectNewtonsoft>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoft>> getQueryDefault;
            string insertedData;
            (getQueryCamelCase, getQueryDefault, insertedData) = this.InsertDataAndGetQueryables<DataObjectNewtonsoft>();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoft>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" } : new DataObjectNewtonsoft() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerMultiSerializer()
        {
            Func<bool, IQueryable<DataObjectMultiSerializer>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectMultiSerializer>> getQueryDefault;
            string insertedData;
            (getQueryCamelCase, getQueryDefault, insertedData) = this.InsertDataAndGetQueryables<DataObjectMultiSerializer>();

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectMultiSerializer>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 12 ? new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" } : new DataObjectMultiSerializer() { NumericField = 12, StringField = "12" }), skipVerification : true, inputData: insertedData),

                    // Negative test case: serializing only field name using custom serializer not currently supported
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectMultiSerializer() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        private (Func<bool, IQueryable<T>>, Func<bool, IQueryable<T>>, string) InsertDataAndGetQueryables<T>() where T : LinqTestObject
        {
            static T createDataObj(int index, bool camelCase)
            {
                T obj = (T)Activator.CreateInstance(typeof(T), new object[]
                {
                    index, index.ToString(), $"{index}-{camelCase}", "Test"
                });
                return obj;
            }

            (Func<bool, IQueryable<T>> getQueryCamelCase, List<T> insertedDataListCamelCase) = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, TestContainer, camelCaseSerialization: true);
            (Func<bool, IQueryable<T>> getQueryDefault, List<T> insertedDataListDefault) = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, TestContainer, camelCaseSerialization: false);

            List<T> insertedDataList = insertedDataListCamelCase.Concat(insertedDataListDefault).ToList();
            string insertedData = JsonConvert.SerializeObject(insertedDataList.Select(item => item.ToString()), new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented });

            return (getQueryCamelCase, getQueryDefault, insertedData);
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input, includeResults: true);
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

            public DataObjectDotNet() { }

            public DataObjectDotNet(double numericFeild, string stringField, string id, string pk)
            {
                this.NumericField = numericFeild;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }
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

            public DataObjectNewtonsoft() { }

            public DataObjectNewtonsoft(double numericFeild, string stringField, string id, string pk)
            {
                this.NumericField = numericFeild;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }
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

            public DataObjectMultiSerializer() { }

            public DataObjectMultiSerializer(double numericFeild, string stringField, string id, string pk)
            {
                this.NumericField = numericFeild;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }
        }
    }
}
