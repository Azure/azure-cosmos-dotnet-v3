//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest
{
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using System;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Collections.Generic;
    using Newtonsoft.Json.Linq;

    [Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestClass]
    public class QueryBaselineTest : BaselineTests<QueryTestInput, QueryTestOutput>
    {
        private static CosmosClient cosmosClient;
        private static Cosmos.Database testDb;
        private static Container testContainer;

        [ClassInitialize]
        public async static Task Initialize(TestContext _)
        {
            string authKey = Utils.ConfigurationManager.AppSettings["MasterKey"];
            Uri uri = new Uri(Utils.ConfigurationManager.AppSettings["GatewayEndpoint"]);
            ConnectionPolicy connectionPolicy = new ConnectionPolicy
            {
                ConnectionMode = ConnectionMode.Gateway,
                EnableEndpointDiscovery = true,
            };

            cosmosClient = TestCommon.CreateCosmosClient((cosmosClientBuilder) =>
            {
                cosmosClientBuilder.WithCustomSerializer(new CustomJsonSerializer(new JsonSerializerSettings()
                {
                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                    // We want to simulate the property not exist so ignoring the null value
                    NullValueHandling = NullValueHandling.Ignore
                })).WithConnectionModeGateway();
            });

            await Util.DeleteAllDatabasesAsync(cosmosClient);
            string dbName = $"{nameof(QueryBaselineTest)}-{Guid.NewGuid().ToString("N")}";
            testDb = await cosmosClient.CreateDatabaseAsync(dbName);
        }

        [ClassCleanup]
        public async static Task CleanUp()
        {
            if (testDb != null)
            {
                await testDb.DeleteStreamAsync();
            }

            cosmosClient?.Dispose();
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            testContainer = await testDb.CreateContainerAsync(new ContainerProperties(id: Guid.NewGuid().ToString(), partitionKeyPath: "/id"));
        }

        [TestCleanup]
        public async Task TestCleanUp()
        {
            await testContainer.DeleteContainerStreamAsync();
        }

        [TestMethod]
        public void TestGroupByQueries()
        {
            string[] documents = new string[]
            {
                @" { ""id"": ""01"", ""name"": ""John"", ""age"": 11, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""02"", ""name"": ""Mady"", ""age"": 15, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""03"", ""name"": ""John"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""04"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""05"", ""name"": ""Fred"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""06"", ""name"": ""Adam"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""07"", ""name"": ""Alex"", ""age"": 13, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""08"", ""name"": ""Fred"", ""age"": 12, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""09"", ""name"": ""Fred"", ""age"": 15, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""10"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""11"", ""name"": ""Fred"", ""age"": 18, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""12"", ""name"": ""Abby"", ""age"": 17, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""13"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""14"", ""name"": ""Ella"", ""age"": 16, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""15"", ""name"": ""Mary"", ""age"": 18, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""16"", ""name"": ""Carl"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""17"", ""name"": ""Mady"", ""age"": 18, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""18"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""19"", ""name"": ""Eric"", ""age"": 16, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""20"", ""name"": ""Ryan"", ""age"": 11, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""21"", ""name"": ""Alex"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""22"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""23"", ""name"": ""John"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""24"", ""name"": ""Dave"", ""age"": 15, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""25"", ""name"": ""Lisa"", ""age"": 11, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""26"", ""name"": ""Zara"", ""age"": 11, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""27"", ""name"": ""Abby"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""28"", ""name"": ""Abby"", ""age"": 13, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""29"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""30"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""31"", ""name"": ""Bill"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""32"", ""name"": ""Bill"", ""age"": 11, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""33"", ""name"": ""Zara"", ""age"": 12, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""34"", ""name"": ""Adam"", ""age"": 13, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""35"", ""name"": ""Bill"", ""age"": 13, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""36"", ""name"": ""Alex"", ""age"": 15, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""37"", ""name"": ""Lucy"", ""age"": 14, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""38"", ""name"": ""Alex"", ""age"": 11, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""39"", ""name"": ""Mike"", ""age"": 15, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""40"", ""name"": ""Eric"", ""age"": 11, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""41"", ""name"": ""John"", ""age"": 12, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""42"", ""name"": ""Ella"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60291 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""43"", ""name"": ""Lucy"", ""age"": 12, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""44"", ""name"": ""Mady"", ""age"": 14, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""45"", ""name"": ""Lori"", ""age"": 17, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 88, 88, 88] } ",
                @" { ""id"": ""46"", ""name"": ""Gary"", ""age"": 17, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""47"", ""name"": ""Eric"", ""age"": 18, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""48"", ""name"": ""Mary"", ""age"": 15, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [23, 11, 11, 66] } ",
                @" { ""id"": ""49"", ""name"": ""Zara"", ""age"": 17, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [90, 45, 62, 21] } ",
                @" { ""id"": ""50"", ""name"": ""Carl"", ""age"": 17, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""51"", ""name"": ""Lori"", ""age"": 11, ""gender"": ""F"", ""team"": ""D"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""52"", ""name"": ""Adam"", ""age"": 13, ""gender"": ""M"", ""team"": ""A"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""53"", ""name"": ""Bill"", ""age"": 16, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""54"", ""name"": ""Zara"", ""age"": 12, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""55"", ""name"": ""Lisa"", ""age"": 16, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""56"", ""name"": ""Ryan"", ""age"": 12, ""gender"": ""M"", ""team"": ""B"", ""address"": { ""city"": ""Chicago"", ""state"": ""IL"", ""zip"": 60292 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""57"", ""name"": ""Abby"", ""age"": 12, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98102 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""58"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32801 }, ""scores"": [38, 66, 54, 25] } ",
                @" { ""id"": ""59"", ""name"": ""Mary"", ""age"": 15, ""gender"": ""F"", ""team"": ""A"", ""address"": { ""city"": ""Seattle"", ""state"": ""WA"", ""zip"": 98101 }, ""scores"": [52, 13, 94, 31] } ",
                @" { ""id"": ""60"", ""name"": ""John"", ""age"": 16, ""gender"": ""M"", ""team"": ""D"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""61"", ""name"": ""Mary"", ""age"": 17, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [12, 10, 12, 10] } ",
                @" { ""id"": ""62"", ""name"": ""Lucy"", ""age"": 12, ""gender"": ""F"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30302 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""63"", ""name"": ""Rose"", ""age"": 14, ""gender"": ""F"", ""team"": ""B"", ""address"": { ""city"": ""Orlando"", ""state"": ""FL"", ""zip"": 32802 }, ""scores"": [88, 47, 90, 76] } ",
                @" { ""id"": ""64"", ""name"": ""Gary"", ""age"": 14, ""gender"": ""M"", ""team"": ""C"", ""address"": { ""city"": ""Atlanta"", ""state"": ""GA"", ""zip"": 30301 }, ""scores"": [88, 47, 90, 76] } ",
            };

            //insert documents into container
            foreach (string document in documents)
            {
                JObject documentObject = JsonConvert.DeserializeObject<JObject>(document);
                string jObjectPartitionKey = "/id".Remove(0, 1);
                JValue pkToken = (JValue)documentObject[jObjectPartitionKey];
                Cosmos.PartitionKey pkValue;

                switch (pkToken.Type)
                {
                    case JTokenType.Integer:
                    case JTokenType.Float:
                        pkValue = new Cosmos.PartitionKey(pkToken.Value<double>());
                        break;
                    case JTokenType.String:
                        pkValue = new Cosmos.PartitionKey(pkToken.Value<string>());
                        break;
                    case JTokenType.Boolean:
                        pkValue = new Cosmos.PartitionKey(pkToken.Value<bool>());
                        break;
                    case JTokenType.Null:
                        pkValue = Cosmos.PartitionKey.Null;
                        break;
                    default:
                        throw new ArgumentException("Unknown partition key type");
                }

                var _ = testContainer.CreateItemAsync(documentObject, pkValue).Result;
            }

            List<(string /*query text*/, string /*description*/)> queryTestCases = new List<(string, string)>()
            {
                // ------------------------------------------
                // Simple property reference
                // ------------------------------------------

                (
                    "SELECT c.age FROM c GROUP BY c.age",
                    "Simple property reference 1" 
                ),

                (
                    "SELECT c.name FROM c GROUP BY c.name",
                    "Simple property reference 2"
                ),

                (
                    "SELECT c.team FROM c GROUP BY c.team",
                    "Simple property reference 3"
                ),

                (
                    "SELECT c.gender FROM c GROUP BY c.gender",
                    "Simple property reference 4"
                ),

                (
                    "SELECT c.id FROM c GROUP BY c.id",
                    "Simple property reference 5"
                ),

                (
                    "SELECT c.age, c.name FROM c GROUP BY c.age, c.name",
                    "Simple property reference 6"
                ),

                // ------------------------------------------
                // With Aggregates
                // ------------------------------------------

                (
                    "SELECT c.age, COUNT(1) as count FROM c GROUP BY c.age",
                    "With SELECT Aggregates 1"
                ),

                (
                    "SELECT c.name, MIN(c.age) AS min_age FROM c GROUP BY c.name",
                    "With SELECT Aggregates 2"
                ),

                (
                    "SELECT c.name, MAX(c.age) AS max_age FROM c GROUP BY c.name",
                    "With SELECT Aggregates 3"
                ),

                (
                    "SELECT c.name, SUM(c.age) AS sum_age FROM c GROUP BY c.name",
                    "With SELECT Aggregates 4"
                ),

                (
                    "SELECT c.name, AVG(c.age) AS avg_age FROM c GROUP BY c.name",
                    "With SELECT Aggregates 5"
                ),

                (
                    "SELECT c.name, Count(1) AS count, Min(c.age) AS min_age, Max(c.age) AS max_age FROM c GROUP BY c.name",
                    "With SELECT Aggregates 6"
                ),

                // ------------------------------------------
                // SELECT VALUE
                // ------------------------------------------

                (
                    "SELECT VALUE c.age FROM c GROUP BY c.age",
                    "With SELECT VALUE"
                ),

                (
                    "SELECT VALUE COUNT(1) FROM c GROUP BY c.age",
                    "With SELECT VALUE Aggregates 1"
                ),

                (
                    "SELECT VALUE MIN(c.age) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregates 2"
                ),

                (
                    "SELECT VALUE MAX(c.age) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregates 3"
                ),

                (
                    "SELECT VALUE AVG(c.age) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregates 4"
                ),

                (
                    "SELECT VALUE SUM(c.age) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregates 5"
                ),

                (
                    "SELECT VALUE { 'Name' : c.name , 'Age' : c.age} FROM c GROUP BY c.name, c.age",
                    "With SELECT VALUE Object"
                ),

                (
                    "SELECT VALUE {'age' : c.age} FROM c GROUP BY {'age' : c.age}",
                    "With SELECT VALUE Object 2"
                ),

                // GROUP BY with SELECT VALUE on undefined aggregate fields
                // sum and count default the counter at 0
                (
                    "SELECT VALUE SUM(c.doesNotExist) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregate on a term that does not exist 1"
                ),

                (
                    "SELECT VALUE COUNT(c.doesNotExist) FROM c GROUP BY c.name",
                    "With SELECT VALUE Aggregate on a term that does not exist 2"
                ),

                (
                    "SELECT VALUE AVG(c.unKnownField) FROM c GROUP BY c.age",
                    "With SELECT VALUE Aggregate on a term that does not exist 3"
                ),


                (
                    "SELECT VALUE MIN(c.unKnownField) FROM c GROUP BY c.age",
                    "With SELECT VALUE Aggregate on a term that does not exist 4"
                ),


                (
                    "SELECT VALUE MAX(c.unKnownField) FROM c GROUP BY c.age",
                    "With SELECT VALUE Aggregate on a term that does not exist 5"
                ),

                /* Composition of Aggregates currently not supported
                (
                    "SELECT VALUE {'count' : COUNT(1)} FROM c GROUP BY c.age",
                    "Compositetion of Aggregate 1"
                ),
               
                (
                    "SELECT VALUE { 'name' : c.name , 'count' : COUNT(1), 'min_age' : MIN(c.age), 'max_age' : c.age} FROM c GROUP BY c.name",
                    "Compositetion of Aggregate 2"
                ),
                */

                // ------------------------------------------
                // Corner Cases
                // ------------------------------------------

                (
                    "SELECT AVG(\"asdf\") as avg_asdf FROM c GROUP BY c.age",
                    "Corner Case 1"
                ),

                (
                    @"SELECT 
                        c.age, 
                        AVG(c.doesNotExist) as undefined_avg,
                        MIN(c.doesNotExist) as undefined_min,
                        MAX(c.doesNotExist) as undefined_max,
                        COUNT(c.doesNotExist) as undefined_count,
                        SUM(c.doesNotExist) as undefined_sum
                    FROM c 
                    GROUP BY c.age",
                    "Corner Case 2"
                ),

                (
                    @"SELECT 
                        c.age, 
                        c.doesNotExist
                    FROM c 
                    GROUP BY c.age, c.doesNotExist",
                    "Corner Case 3"
                ),

                // ------------------------------------------
                // GROUP BY partition key
                // ------------------------------------------

                (
                    "SELECT VALUE c.id FROM c GROUP BY c.id",
                    "With Partition Key"
                ),

                // ------------------------------------------
                // GROUP BY undefined
                // ------------------------------------------

                (
                    "SELECT COUNT(1) as count, c.DoesNotExist as DoesNotExist FROM c GROUP BY c.DoesNotExist",
                    "Group By Undefined"
                ),
            };

            List<QueryTestInput> inputs = new List<QueryTestInput>();
            foreach ((string query, string description) in queryTestCases)
            {
                QueryTestInput input = new QueryTestInput(description, query);
                inputs.Add(input);
            }

            this.ExecuteTestSuite(inputs);
        }

        public override QueryTestOutput ExecuteTest(QueryTestInput input)
        {
            FeedIterator<dynamic> iterator = testContainer.GetItemQueryIterator<dynamic>(input.QueryText);

            List<object> results = new List<object>();
            while (iterator.HasMoreResults)
            {
                FeedResponse<dynamic> response = iterator.ReadNextAsync().Result;
                foreach(var item in response)
                {
                    results.Add(item);
                }
            }

            return new QueryTestOutput(serializedResults: JsonConvert.SerializeObject(results));
        }
    }


    public sealed class QueryTestInput : BaselineTestInput
    {
        internal string QueryText;

        internal QueryTestInput(
            string description,
            string queryText)
            : base(description)
        {
            this.QueryText = queryText;
        }
        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (xmlWriter == null)
            {
                throw new ArgumentNullException($"{nameof(xmlWriter)} cannot be null.");
            }

            xmlWriter.WriteStartElement("Description");
            xmlWriter.WriteCData(this.Description);
            xmlWriter.WriteEndElement();
            xmlWriter.WriteStartElement("Expression");
            xmlWriter.WriteCData(this.QueryText);
            xmlWriter.WriteEndElement();
        }
    }

    public class QueryTestOutput : BaselineTestOutput
    {
        internal string Results { get; }

        internal QueryTestOutput(string serializedResults)
        {
            this.Results = serializedResults;
        }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            if (this.Results != null)
            {
                xmlWriter.WriteStartElement("Results");
                xmlWriter.WriteCData(this.Results);
                xmlWriter.WriteEndElement();
            }
        }
    }
}
