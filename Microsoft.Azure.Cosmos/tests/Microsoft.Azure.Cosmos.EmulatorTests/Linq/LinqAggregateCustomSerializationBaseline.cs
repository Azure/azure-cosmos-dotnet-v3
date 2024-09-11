//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Services.Management.Tests.LinqProviderTests.LinqAggregateFunctionBaselineTests;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class LinqAggregateCustomSerializationBaseline : BaselineTests<LinqAggregateInput, LinqAggregateOutput>
    {
        private static CosmosSerializer customCosmosLinqSerializer;
        private static CosmosClient clientLinq;
        private static Cosmos.Database testDbLinq;
        private static Container testContainerLinq;
        private static IQueryable lastExecutedScalarQuery;

        private static CosmosSerializer customCosmosSerializer;
        private static CosmosClient client;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        private static CosmosClient stjClient;
        private static Cosmos.Database testDbSTJ;
        private static Container testContainerSTJ;

        [ClassInitialize]
        public async static Task Initialize(TestContext textContext)
        {
            customCosmosLinqSerializer = new SystemTextJsonLinqSerializer(new JsonSerializerOptions());
            clientLinq = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithCustomSerializer(customCosmosLinqSerializer));

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            clientLinq.DocumentClient.OnExecuteScalarQueryCallback = q => lastExecutedScalarQuery = q;

            string dbName = $"{nameof(LinqAggregateCustomSerializationBaseline)}-{Guid.NewGuid():N}";
            testDbLinq = await clientLinq.CreateDatabaseAsync(dbName);
            testContainerLinq = testDbLinq.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk")).Result;

            customCosmosSerializer = new SystemTextJsonSerializer(new JsonSerializerOptions());

            client = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithCustomSerializer(customCosmosSerializer));

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            client.DocumentClient.OnExecuteScalarQueryCallback = q => lastExecutedScalarQuery = q;

            dbName = $"{nameof(LinqAggregateCustomSerializationBaseline)}-{Guid.NewGuid():N}";
            testDb = await client.CreateDatabaseAsync(dbName);
            testContainer = testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk")).Result;

            stjClient = TestCommon.CreateCosmosClient((cosmosClientBuilder)
                => cosmosClientBuilder.WithSystemTextJsonSerializerOptions(
                    new JsonSerializerOptions()),
                    useCustomSeralizer: false);

            // Set a callback to get the handle of the last executed query to do the verification
            // This is neede because aggregate queries return type is a scalar so it can't be used 
            // to verify the translated LINQ directly as other queries type.
            stjClient.DocumentClient.OnExecuteScalarQueryCallback = q => lastExecutedScalarQuery = q;

            dbName = $"{nameof(LinqAggregateCustomSerializationBaseline)}-{Guid.NewGuid():N}";
            testDbSTJ = await stjClient.CreateDatabaseAsync(dbName);
            testContainerSTJ = testDbSTJ.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/Pk")).Result;
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDbLinq != null)
            {
                await testDbLinq.DeleteStreamAsync();
            }

            clientLinq?.Dispose();

            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            client?.Dispose();
        }

        [TestMethod]
        [Owner("mayapainter")]
        public void TestAggregateQueriesWithCustomSerializer()
        {
            static DataObjectDotNet createDataObj(int index, bool camelCase)
            {
                DataObjectDotNet obj = new DataObjectDotNet
                {
                    NumericField = index,
                    StringField = index.ToString(),
                    ArrayField = new int[] { 1, 2, 3, 4, 5 },
                    id = Guid.NewGuid().ToString(),
                    Pk = "Test"
                };
                return obj;
            }

            List<Func<bool, IQueryable<DataObjectDotNet>>> getQueryList = new List<Func<bool, IQueryable<DataObjectDotNet>>> 
            {
                LinqTestsCommon.GenerateSerializationTestCosmosData<DataObjectDotNet>(createDataObj, 5, testContainerLinq, new CosmosLinqSerializerOptions()),
                LinqTestsCommon.GenerateSerializationTestCosmosData<DataObjectDotNet>(createDataObj, 5, testContainer, new CosmosLinqSerializerOptions()),
                LinqTestsCommon.GenerateSerializationTestCosmosData<DataObjectDotNet>(createDataObj, 5, testContainerSTJ, new CosmosLinqSerializerOptions())
            };

            Dictionary<string, int> serializerIndexes = new()
            {
                { nameof(SystemTextJsonLinqSerializer), 0 },
                { nameof(SystemTextJsonSerializer), 1},
                { nameof(CosmosSystemTextJsonSerializer), 2}
            };

            List<LinqAggregateInput> inputs = new List<LinqAggregateInput>();

            foreach (KeyValuePair<string, int> entry in serializerIndexes)
            {
                Func<bool, IQueryable<DataObjectDotNet>> getQuery = getQueryList[entry.Value];

                inputs.Add(new LinqAggregateInput(
                    "Avg, Serializer Name: " + entry.Key, b => getQuery(b)
                    .Average(doc => doc.NumericField)));

                inputs.Add(new LinqAggregateInput(
                    "Sum, Serializer Name: " + entry.Key, b => getQuery(b)
                    .Sum(doc => doc.NumericField)));

                inputs.Add(new LinqAggregateInput(
                    "Select many -> Filter -> Select -> Average, Serializer Name: " + entry.Key, b => getQuery(b)
                    .SelectMany(doc => doc.ArrayField.Where(m => (m % 3) == 0).Select(m => m)).Average()));

                inputs.Add(new LinqAggregateInput(
                    "Select number -> Skip -> Count, Serializer Name: " + entry.Key, b => getQuery(b)
                    .Select(f => f.NumericField).Skip(2).Count()));

                inputs.Add(new LinqAggregateInput(
                    "Select number -> Min w/ mapping", b => getQuery(b)
                    .Select(doc => doc.NumericField).Min(num => num)));
            }

            this.ExecuteTestSuite(inputs);
        }

        public override LinqAggregateOutput ExecuteTest(LinqAggregateInput input)
        {
            lastExecutedScalarQuery = null;
            Func<bool, object> compiledQuery = input.expression.Compile();

            string errorMessage = null;
            string query = string.Empty;
            try
            {
                object queryResult;
                try
                {
                    queryResult = compiledQuery(true);
                }
                finally
                {
                    Assert.IsNotNull(lastExecutedScalarQuery, "lastExecutedScalarQuery is not set");

                    query = JObject
                        .Parse(lastExecutedScalarQuery.ToString())
                        .GetValue("query", StringComparison.Ordinal)
                        .ToString();
                }
            }
            catch (Exception e)
            {
                errorMessage = LinqTestsCommon.BuildExceptionMessageForTest(e);
            }

            return new LinqAggregateOutput(query, errorMessage);
        }

        private class DataObjectDotNet : LinqTestObject
        {
            [JsonPropertyName("NumberValueDotNet")]
            public double NumericField { get; set; }

            [JsonPropertyName("StringValueDotNet")]
            public string StringField { get; set; }

            [JsonPropertyName("ArrayValuesDotNet")]
            public int[] ArrayField { get; set; }

            public string id { get; set; }

            public string Pk { get; set; }

            public DataObjectDotNet() { }

            public DataObjectDotNet(double numericField, string stringField, int[] arrayField, string id, string pk)
            {
                this.NumericField = numericField;
                this.StringField = stringField;
                this.ArrayField = arrayField;
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
