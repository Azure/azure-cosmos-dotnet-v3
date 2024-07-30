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
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    [SDK.EmulatorTests.TestClass]
    public class LinqTranslationWithCustomSerializerBaseline : BaselineTests<LinqTestInput, LinqTestOutput>
    {
        private static CosmosClient CosmosLinqClient;
        private static Database TestDbLinq;
        private static Container TestLinqContainer;

        private static CosmosClient CosmosClient;
        private static Database TestDb;
        private static Container TestContainer;

        private static CosmosClient CosmosDefaultSTJClient;
        private static Database TestDbSTJDefault;
        private static Container TestSTJContainer;

        private const int RecordCount = 3;
        private const int MaxValue = 500;
        private const int MaxStringLength = 100;
        private const int PropertyCount = 4;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            CosmosLinqClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithCustomSerializer(new SystemTextJsonLinqSerializer(new JsonSerializerOptions())));

            string dbNameLinq = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid():N}";
            TestDbLinq = await CosmosLinqClient.CreateDatabaseAsync(dbNameLinq);

            CosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                 => cosmosClientBuilder.WithCustomSerializer(new SystemTextJsonSerializer(new JsonSerializerOptions())));

            string dbName = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid():N}";
            TestDb = await CosmosClient.CreateDatabaseAsync(dbName);

            CosmosDefaultSTJClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder
                    .WithSystemTextJsonSerializerOptions(
                        new JsonSerializerOptions()),
                useCustomSeralizer: false);

            string dbNameSTJ = $"{nameof(LinqTranslationBaselineTests)}-{Guid.NewGuid():N}";
            TestDbSTJDefault = await CosmosDefaultSTJClient.CreateDatabaseAsync(dbNameSTJ);
        }

        [ClassCleanup]
        public async static Task Cleanup()
        {
            if (TestDbLinq != null)
            {
                await TestDbLinq.DeleteStreamAsync();
            }

            if (TestDb != null)
            {
                await TestDb.DeleteStreamAsync();
            }

            if (TestDbSTJDefault != null)
            {
                await TestDbSTJDefault.DeleteStreamAsync();
            }
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            TestSTJContainer = await TestDbSTJDefault.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
            TestLinqContainer = await TestDbLinq.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
            TestContainer = await TestDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk"));
        }

        [TestCleanup]
        public async Task TestCleanup()
        {
            await TestLinqContainer.DeleteContainerStreamAsync();
            await TestSTJContainer.DeleteContainerStreamAsync();
            await TestContainer.DeleteContainerStreamAsync();
        }

        public override LinqTestOutput ExecuteTest(LinqTestInput input)
        {
            return LinqTestsCommon.ExecuteTest(input, serializeResultsInBaseline: true);
        }

        [TestMethod]
        public void TestMemberInitializerDotNetCustomSerializer()
        {
            Func<bool, IQueryable<DataObjectDotNet>> getQuery;
            (_, getQuery) = this.InsertDataAndGetQueryables<DataObjectDotNet>(true, TestLinqContainer);

            string insertedData = this.GetInsertedData(TestLinqContainer).Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ constant value", b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ DataObject initializer with constant value", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Deeper than top level reference", b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectDotNet() { NumericField = 1, StringField = "1" } : new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ DataObject initializer with member initialization", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData),
                new LinqTestInput("OrderBy query", b => getQuery(b).Select(x => x).OrderBy(x => x.NumericField).Take(5), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Conditional", b => getQuery(b).Select(c => c.NumericField > 1 ? "true" : "false"), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ nullable property", b => getQuery(b).Where(doc => doc.DateTimeField != null), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField != null), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ non-null nullable property", b => getQuery(b).Where(doc => doc.DateTimeField == new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ non-null nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField == DataType.Point), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ string null comparison", b => getQuery(b).Where(doc => doc.StringField != null), skipVerification : true, inputData: insertedData),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoft()
        {
            Func<bool, IQueryable<DataObjectNewtonsoft>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoft>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoft>(false, TestContainer);

            string insertedData = this.GetInsertedData(TestContainer).Result;

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
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoft() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ nullable property, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.DateTimeField != null), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ nullable enum, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.DataTypeField != null), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ non-null nullable property", b => getQuery(b).Where(doc => doc.DateTimeField == new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ non-null nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField == DataType.Point), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ string null comparison, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.StringField != null), skipVerification : true, inputData: insertedData),
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerDotNetDefaultSerializer()
        {
            Func<bool, IQueryable<DataObjectDotNet>> getQuery;
            (_, getQuery) = this.InsertDataAndGetQueryables<DataObjectDotNet>(true, TestSTJContainer);

            string insertedData = this.GetInsertedData(TestSTJContainer).Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>
            {
                new LinqTestInput("Filter w/ constant value", b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ DataObject initializer with constant value", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Select w/ DataObject initializer", b => getQuery(b).Select(doc => new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Deeper than top level reference", b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectDotNet() { NumericField = 1, StringField = "1" } : new DataObjectDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ DataObject initializer with member initialization", b => getQuery(b).Where(doc => doc == new DataObjectDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData),
                new LinqTestInput("OrderBy query", b => getQuery(b).Select(x => x).OrderBy(x => x.NumericField).Take(5), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Conditional", b => getQuery(b).Select(c => c.NumericField > 1 ? "true" : "false"), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ nullable property", b => getQuery(b).Where(doc => doc.DateTimeField != null), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField != null), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ non-null nullable property", b => getQuery(b).Where(doc => doc.DateTimeField == new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ non-null nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField == DataType.Point), skipVerification : true, inputData: insertedData),
                new LinqTestInput("Filter w/ string null comparison", b => getQuery(b).Where(doc => doc.StringField != null), skipVerification : true, inputData: insertedData),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerDataMember()
        {
            Func<bool, IQueryable<DataObjectDataMember>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectDataMember>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectDataMember>(false, TestContainer);

            string insertedData = this.GetInsertedData(TestContainer).Result;

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
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDataMember() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ nullable property, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.DateTimeField != null), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ nullable enum, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.DataTypeField != null), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ non-null nullable property", b => getQuery(b).Where(doc => doc.DateTimeField == new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ non-null nullable enum", b => getQuery(b).Where(doc => doc.DataTypeField == DataType.Point), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ string null comparison, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.StringField != null), skipVerification : true, inputData: insertedData),
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoftDotNet()
        {
            Func<bool, IQueryable<DataObjectNewtonsoftDotNet>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoftDotNet>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoftDotNet>(false, TestContainer);
            
            string insertedData = this.GetInsertedData(TestContainer).Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoftDotNet>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoftDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectNewtonsoftDotNet() { NumericField = 1, StringField = "1" } : new DataObjectNewtonsoftDotNet() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDotNet() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerNewtonsoftDataMember()
        {
            Func<bool, IQueryable<DataObjectNewtonsoftDataMember>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectNewtonsoftDataMember>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectNewtonsoftDataMember>(false, TestContainer);

            string insertedData = this.GetInsertedData(TestContainer).Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectNewtonsoftDataMember>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
                {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectNewtonsoftDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectNewtonsoftDataMember() { NumericField = 1, StringField = "1" } : new DataObjectNewtonsoftDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectNewtonsoftDataMember() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
                };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void TestMemberInitializerDotNetDataMember()
        {
            Func<bool, IQueryable<DataObjectDotNetDataMember>> getQueryCamelCase;
            Func<bool, IQueryable<DataObjectDotNetDataMember>> getQueryDefault;
            (getQueryCamelCase, getQueryDefault) = this.InsertDataAndGetQueryables<DataObjectDotNetDataMember>(false, TestContainer);

            string insertedData = this.GetInsertedData(TestContainer).Result;

            List<LinqTestInput> inputs = new List<LinqTestInput>();
            foreach (bool useCamelCaseSerializer in new bool[] { true, false })
            {
                Func<bool, IQueryable<DataObjectDotNetDataMember>> getQuery = useCamelCaseSerializer ? getQueryCamelCase : getQueryDefault;

                List<LinqTestInput> camelCaseSettingInputs = new List<LinqTestInput>
            {
                    new LinqTestInput("Filter w/ constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc.NumericField == 1), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with constant value, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNetDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Select w/ DataObject initializer, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => new DataObjectDotNetDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Deeper than top level reference, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Select(doc => doc.NumericField > 1 ? new DataObjectDotNetDataMember() { NumericField = 1, StringField = "1" } : new DataObjectDotNetDataMember() { NumericField = 1, StringField = "1" }), skipVerification : true, inputData: insertedData),
                    new LinqTestInput("Filter w/ DataObject initializer with member initialization, camelcase = " + useCamelCaseSerializer, b => getQuery(b).Where(doc => doc == new DataObjectDotNetDataMember() { NumericField = doc.NumericField, StringField = doc.StringField }).Select(b => "A"), skipVerification : true, inputData: insertedData)
            };

                inputs.AddRange(camelCaseSettingInputs);
            }

            this.ExecuteTestSuite(inputs);
        }

        private (Func<bool, IQueryable<T>>, Func<bool, IQueryable<T>>) InsertDataAndGetQueryables<T>(bool customSerializer, Container container) where T : LinqTestObject
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
            };

            CosmosLinqSerializerOptions linqSerializerOptionsDefault = new CosmosLinqSerializerOptions();

            Func<bool, IQueryable<T>> getQueryCamelCase = null;
            if (!customSerializer)
            {
                getQueryCamelCase = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, container, linqSerializerOptionsCamelCase);
            }

            Func<bool, IQueryable<T>> getQueryDefault = LinqTestsCommon.GenerateSerializationTestCosmosData(createDataObj, RecordCount, container, linqSerializerOptionsDefault);

            return (getQueryCamelCase, getQueryDefault);
        }

        private async Task<string> GetInsertedData(Container container)
        {
            List<string> insertedDataList = new List<string>();
            using (FeedIterator feedIterator = container.GetItemQueryStreamIterator("SELECT * FROM c"))
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

            return JsonConvert.SerializeObject(insertedDataList.Select(item => item), new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented });
        }

        private class DataObjectDotNet : LinqTestObject
        {
            [JsonPropertyName("NumberValueDotNet")]
            public double NumericField { get; set; }

            [JsonPropertyName("StringValueDotNet")]
            public string StringField { get; set; }

            [System.Text.Json.Serialization.JsonIgnore]
            public string IgnoreField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            [JsonPropertyName("DateTimeFieldDotNet")]
            public DateTime? DateTimeField { get; set; }

            [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
            public DataType? DataTypeField { get; set; }

            public DataObjectDotNet() { }

            public DataObjectDotNet(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.IgnoreField = "Ignore";
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

            [Newtonsoft.Json.JsonIgnore]
            public string IgnoreField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            [Newtonsoft.Json.JsonConverter(typeof(IsoDateTimeConverter))]
            public DateTime? DateTimeField { get; set; }

            [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
            public DataType? DataTypeField { get; set; }

            public DataObjectNewtonsoft() { }

            public DataObjectNewtonsoft(double numericField, string stringField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.IgnoreField = "ignore";
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

            [DataMember(Name = "DateTimeFieldDataMember")]
            public DateTime? DateTimeField { get; set; }

            [DataMember(Name = "DataTypeFieldDataMember")]
            public DataType? DataTypeField { get; set; }

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

        private class DataObjectNewtonsoftDotNet : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectNewtonsoftDotNet() { }

            public DataObjectNewtonsoftDotNet(double numericField, string stringField, string id, string pk)
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
        private class DataObjectNewtonsoftDataMember : LinqTestObject
        {
            [Newtonsoft.Json.JsonProperty(PropertyName = "NumberValueNewtonsoft")]
            [DataMember(Name = "NumericFieldDataMember")]
            public double NumericField { get; set; }

            [Newtonsoft.Json.JsonProperty(PropertyName = "StringValueNewtonsoft")]
            [DataMember(Name = "StringFieldDataMember")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectNewtonsoftDataMember() { }

            public DataObjectNewtonsoftDataMember(double numericField, string stringField, string id, string pk)
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
        private class DataObjectDotNetDataMember : LinqTestObject
        {
            [DataMember(Name = "NumericFieldDataMember")]
            [JsonPropertyName("numberValueDotNet")]
            public double NumericField { get; set; }

            [DataMember(Name = "StringFieldDataMember")]
            [JsonPropertyName("stringValueDotNet")]
            public string StringField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectDotNetDataMember() { }

            public DataObjectDotNetDataMember(double numericField, string stringField, string id, string pk)
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
