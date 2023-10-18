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
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using BaselineTest;
    using global::Azure.Core.Serialization;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [SDK.EmulatorTests.TestClass]
    public class LinqTranslationWithCustomSerializerBaseline : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient CosmosClient;
        private static Database TestDb;
        private static Container TestContainer;

        private const int RecordCount = 3;
        private const int MaxValue = 500;
        private const int MaxStringLength = 100;
        private const int PropertyCount = 4;

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

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input, serializeResultsInBaseline: true);
        }

        [TestMethod]
        public void TestMemberInitializerDotNet()
        {
            Func<bool, IQueryable<DataObjectDotNet>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectDotNet>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectDotNet>();

            string insertedData = this.GetInsertedData().Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectDotNet>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    // TODO (10/13/23): extend this and other tests cases as more LINQ features are added (GROUP BY, etc.)
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectDotNet() { NumericField = 1, StringField = "1" } : new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
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
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoft>();

            string insertedData = this.GetInsertedData().Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoft>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoft() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectNewtonsoft() { NumericField = 1, StringField = "1" } : new DataObjectNewtonsoft() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerDataMember()
        {
            Func<bool, IQueryable<DataObjectDataMember>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectDataMember>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectDataMember>();

            string insertedData = this.GetInsertedData().Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectDataMember>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectDataMember() { NumericField = 1, StringField = "1" } : new DataObjectDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDataMember() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoftDotNetSerializer()
        {
            Func<bool, IQueryable<DataObjectNewtonsoftDotNetSerializer>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoftDotNetSerializer>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoftDotNetSerializer>();
            
            string insertedData = this.GetInsertedData().Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoftDotNetSerializer>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDotNetSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoftDotNetSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectNewtonsoftDotNetSerializer() { NumericField = 1, StringField = "1" } : new DataObjectNewtonsoftDotNetSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDotNetSerializer() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoftDataMemberSerializer()
        {
            Func<bool, IQueryable<DataObjectNewtonsoftDataMemberSerializer>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoftDataMemberSerializer>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoftDataMemberSerializer>();

            string insertedData = this.GetInsertedData().Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoftDataMemberSerializer>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDataMemberSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoftDataMemberSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectNewtonsoftDataMemberSerializer() { NumericField = 1, StringField = "1" } : new DataObjectNewtonsoftDataMemberSerializer() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDataMemberSerializer() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        private (Func<bool, IQueryable<T>>, Func<bool, IQueryable<T>>) InsertDataAndGetQueryables<T>() where T : LinqTestObject
        {
            static T createDataObj(int index, bool camelCase)
            {
                T obj = (T)Activator.CreateInstance(typeof(T), new object[]
                {
                    index, index.ToString(), $"{index}-{camelCase}", "Test"
                });
                return obj;
            }

            CosmosLinqSerializerOptions linqSerializerOptionsCamelCase = new CosmosLinqSerializerOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase,
                CustomCosmosSerializer = new SystemTextJsonSerializer(new JsonSerializerOptions())
            };

            CosmosLinqSerializerOptions linqSerializerOptionsDefault = new CosmosLinqSerializerOptions
            {
                CustomCosmosSerializer = new SystemTextJsonSerializer(new JsonSerializerOptions())
            };

            Func<bool, IQueryable<T>> getQueryCamelCase = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, TestContainer, linqSerializerOptionsCamelCase);
            Func<bool, IQueryable<T>> getQueryDefault = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, TestContainer, linqSerializerOptionsDefault);

            return (getQueryCamelCase, getQueryDefault);
        }

        private async Task<string> GetInsertedData()
        {
            List<string> insertedDataList = new List<string>();
            using (FeedIterator feedIterator = TestContainer.GetItemQueryStreamIterator("SELECT * FROM c"))
            {
                while (feedIterator.HasMoreResults)
                {
                    using (ResponseMessage response = await feedIterator.ReadNextAsync())
                    {
                        response.EnsureSuccessStatusCode();
                        using (StreamReader streamReader = new StreamReader(response.Content))
                        using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
                        {
                            // manual parsing of response object to preserve property names
                            JObject queryResponseObject = await JObject.LoadAsync(jsonTextReader);
                            IEnumerable<JToken> info = queryResponseObject["Documents"].AsEnumerable();

                            foreach (JToken docToken in info)
                            {
                                string documentString = "{";
                                for (int index = 0; index < PropertyCount; index++)
                                {
                                    documentString += index == 0 ? String.Empty : ", ";
                                    documentString += docToken.ElementAt(index).ToString();
                                }
                                documentString += "}";
                                insertedDataList.Add(documentString);
                            }
                        }
                    }
                }
            }

            string insertedData = JsonConvert.SerializeObject(insertedDataList.Select(item => item), new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented });
            return insertedData;
        }

        class SystemTextJsonSerializer : CosmosSerializer
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

        private class DataObjectDotNet : LinqTestObject
        {
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectDotNet() { }

            public DataObjectDotNet(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }

            public override string ToString()
            {
                return $"{{NumericField:{this.NumericField},StringField:{this.StringField},id:{this.id},Pk:{this.Pk}}}";
            }
        }

        private class DataObjectNewtonsoft : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectNewtonsoft() { }

            public DataObjectNewtonsoft(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }

            public override string ToString()
            {
                return $"{{NumericField:{this.NumericField},StringField:{this.StringField},id:{this.id},Pk:{this.Pk}}}";
            }
        }

        [DataContract]
        private class DataObjectDataMember : LinqTestObject
        {
            [DataMember(Name = "NumericFieldDataMember")]
            public double NumericField { get; set; }

            [DataMember(Name = "StringFieldDataMember")]
            public string StringField { get; set; }

            [DataMember(Name = "id")]
            public string id { get; set; }

            [DataMember(Name = "Pk")]
            public string Pk { get; set; }

            public DataObjectDataMember() { }

            public DataObjectDataMember(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }

            public override string ToString()
            {
                return $"{{NumericField:{this.NumericField},StringField:{this.StringField},id:{this.id},Pk:{this.Pk}}}";
            }
        }

        private class DataObjectNewtonsoftDotNetSerializer : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectNewtonsoftDotNetSerializer() { }

            public DataObjectNewtonsoftDotNetSerializer(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }

            public override string ToString()
            {
                return $"{{NumericField:{this.NumericField},StringField:{this.StringField},id:{this.id},Pk:{this.Pk}}}";
            }
        }

        private class DataObjectNewtonsoftDataMemberSerializer : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            [DataMember(Name = "NumericFieldDataMember")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            [DataMember(Name = "StringFieldDataMember")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectNewtonsoftDataMemberSerializer() { }

            public DataObjectNewtonsoftDataMemberSerializer(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.id = id;
                this.Pk = pk;
            }

            public override string ToString()
            {
                return $"{{NumericField:{this.NumericField},StringField:{this.StringField},id:{this.id},Pk:{this.Pk}}}";
            }
        }
    }
}
