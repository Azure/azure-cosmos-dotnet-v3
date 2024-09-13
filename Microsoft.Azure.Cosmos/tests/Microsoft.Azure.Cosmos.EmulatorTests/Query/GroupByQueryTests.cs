namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Drawing.Printing;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("Query")]
    public sealed class GroupByQueryTests : QueryTestsBase
    {
        [TestMethod]
        public async Task TestGroupByQueryAsync()
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

            await this.CreateIngestQueryDeleteAsync(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestGroupByQueryHelper1);
        }

        private async Task TestGroupByQueryHelper1(
            Container container,
            IReadOnlyList<CosmosObject> documents)
        {
            List<(string, IReadOnlyList<CosmosElement>)> queryAndExpectedResultsList = new List<(string, IReadOnlyList<CosmosElement>)>()
            {
                // ------------------------------------------
                // Simple property reference
                // ------------------------------------------

                (
                    "SELECT c.age FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", grouping.Key }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.team FROM c GROUP BY c.team",
                    documents
                        .GroupBy(document => document["team"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "team", grouping.Key }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.gender FROM c GROUP BY c.gender",
                    documents
                        .GroupBy(document => document["gender"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "gender", grouping.Key }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.id FROM c GROUP BY c.id",
                    documents
                        .GroupBy(document => document["id"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "id", grouping.Key }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.age, c.name FROM c GROUP BY c.age, c.name",
                    documents
                        .GroupBy(document => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", document["age"] },
                                { "name", document["name"] },
                            }))
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", (grouping.Key as CosmosObject)["age"] },
                                { "name", (grouping.Key as CosmosObject)["name"] }
                            }))
                        .ToList()
                ),

                // ------------------------------------------
                // With Aggregates
                // ------------------------------------------

                (
                    "SELECT c.age, COUNT(1) as count FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", grouping.Key },
                                { "count", CosmosNumber64.Create(grouping.Count()) }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name, MIN(c.age) AS min_age FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "min_age", CosmosNumber64.Create(grouping.Min(document => document["age"].ToDouble())) }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name, MAX(c.age) AS max_age FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "max_age", CosmosNumber64.Create(grouping.Max(document => document["age"].ToDouble())) }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name, SUM(c.age) AS sum_age FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "sum_age", CosmosNumber64.Create(grouping.Sum(document => document["age"].ToDouble())) }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name, AVG(c.age) AS avg_age FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "avg_age", CosmosNumber64.Create(grouping.Average(document => document["age"].ToDouble())) }
                            }))
                        .ToList()
                ),

                (
                    "SELECT c.name, Count(1) AS count, Min(c.age) AS min_age, Max(c.age) AS max_age FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "count", CosmosNumber64.Create(grouping.Count()) },
                                { "min_age", CosmosNumber64.Create(grouping.Min(document => document["age"].ToDouble())) },
                                { "max_age", CosmosNumber64.Create(grouping.Max(document => document["age"].ToDouble())) },
                            }))
                        .ToList()
                ),

                // ------------------------------------------
                // SELECT VALUE
                // ------------------------------------------

                (
                    "SELECT VALUE c.age FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => grouping.Key)
                        .ToList()
                ),

                (
                    "SELECT VALUE COUNT(1) FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosNumber64.Create(grouping.Count()))
                        .ToList()
                ),

                (
                    "SELECT VALUE MIN(c.age) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(grouping.Min(document => document["age"].ToDouble())))
                        .ToList()
                ),

                (
                    "SELECT VALUE MAX(c.age) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(grouping.Max(document => document["age"].ToDouble())))
                        .ToList()
                ),

                (
                    "SELECT VALUE AVG(c.age) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(grouping.Average(document => document["age"].ToDouble())))
                        .ToList()
                ),

                (
                    "SELECT VALUE SUM(c.age) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(grouping.Sum(document => document["age"].ToDouble())))
                        .ToList()
                ),

                (
                    "SELECT VALUE { 'Name' : c.name , 'Age' : c.age} FROM c GROUP BY c.name, c.age",
                    documents
                        .GroupBy(document => new { key1 = document["name"], key2 = document["age"]})
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "Name", grouping.Key.key1 },
                                { "Age", grouping.Key.key2},
                            }))
                        .ToList()
                ),

                (
                    "SELECT VALUE {'age' : c.age} FROM c GROUP BY {'age' : c.age}",
                    documents
                        .GroupBy(document => new { age = document["age"]})
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", grouping.Key.age},
                            }))
                        .ToList()
                ),

                // GROUP BY with SELECT VALUE on undefined aggregate fields
                // sum and count default the counter at 0
                (
                    "SELECT VALUE SUM(c.doesNotExist) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(0))
                        .ToList()
                ),

                (
                    "SELECT VALUE COUNT(c.doesNotExist) FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosNumber64.Create(0))
                        .ToList()
                ),

                (
                    "SELECT VALUE AVG(c.unKnownField) FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => new { age = document["age"]})
                        .Select(grouping => CosmosUndefined.Create())
                        .ToList()
                ),


                (
                    "SELECT VALUE MIN(c.unKnownField) FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => new { age = document["age"]})
                        .Select(grouping => CosmosUndefined.Create())
                        .ToList()
                ),


                (
                    "SELECT VALUE MAX(c.unKnownField) FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => new { age = document["age"]})
                        .Select(grouping => CosmosUndefined.Create())
                        .ToList()
                ),

                /* Composition of Aggregates currently not supported
                (
                    "SELECT VALUE {'count' : COUNT(1)} FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => new { age = document["age"]})
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "count", CosmosNumber64.Create(grouping.Count()) }
                            }))
                        .ToList()
                ),
               
                (
                    "SELECT VALUE { 'name' : c.name , 'count' : COUNT(1), 'min_age' : MIN(c.age), 'max_age' : c.age} FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "name", grouping.Key },
                                { "count", CosmosNumber64.Create(grouping.Count()) },
                                { "min_age", CosmosNumber64.Create(grouping.Min(document => document["age"].ToDouble())) },
                                { "max_age", CosmosNumber64.Create(grouping.Max(document => document["age"].ToDouble())) },
                            }))
                        .ToList()
                ),
                */

                // ------------------------------------------
                // Corner Cases
                // ------------------------------------------

                (
                    "SELECT AVG(\"asdf\") as avg_asdf FROM c GROUP BY c.age",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosObject.Create(new Dictionary<string, CosmosElement>()))
                        .ToList()
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
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", grouping.Key },
                                // sum and count default the counter at 0
                                { "undefined_sum", CosmosNumber64.Create(0) },
                                { "undefined_count", CosmosNumber64.Create(0) },
                            }))
                        .ToList()
                ),

                (
                    @"SELECT 
                        c.age, 
                        c.doesNotExist
                    FROM c 
                    GROUP BY c.age, c.doesNotExist",
                    documents
                        .GroupBy(document => document["age"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "age", grouping.Key }
                            }))
                        .ToList()
                ),

                // ------------------------------------------
                // GROUP BY partition key
                // ------------------------------------------

                (
                    "SELECT VALUE c.id FROM c GROUP BY c.id",
                    documents
                        .GroupBy(document => document["id"])
                        .Select(grouping => grouping.Key)
                        .ToList()
                ),

                // ------------------------------------------
                // GROUP BY undefined
                // ------------------------------------------

                (
                    "SELECT COUNT(1) as count, c.DoesNotExist as DoesNotExist FROM c GROUP BY c.DoesNotExist",
                    new[]
                    {
                        CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "count", CosmosNumber64.Create(documents.Count) }
                            })
                    }
                ),
            };

            // Test query correctness.
            foreach ((string query, IReadOnlyList<CosmosElement> expectedResults) in queryAndExpectedResultsList)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                    {
                        MaxConcurrency = 2,
                        MaxItemCount = maxItemCount,
                        MaxBufferedItemCount = 100,
                    };

                    List<CosmosElement> actualWithoutContinuationTokens = await QueryTestsBase.QueryWithoutContinuationTokensAsync<CosmosElement>(
                        container,
                        query,
                        queryRequestOptions);
                    HashSet<CosmosElement> actualWithoutContinuationTokensSet = new HashSet<CosmosElement>(actualWithoutContinuationTokens);

                    HashSet<CosmosElement> expectedSet = new HashSet<CosmosElement>(expectedResults);

                    Assert.IsTrue(
                       actualWithoutContinuationTokensSet.SetEquals(expectedSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual {JsonConvert.SerializeObject(actualWithoutContinuationTokensSet)}" +
                       $"Expected: {JsonConvert.SerializeObject(expectedSet)}");
                }
            }

            List<(string, List<CosmosElement>)> queryAndExpectedResultsListArrayAggregates = new List<(string, List<CosmosElement>)>()
            {
                (
                    "SELECT c.name AS Name, MAKELIST(c.age) AS AgeList FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "Name", grouping.Key },
                                { "AgeList", CosmosArray.Create(grouping.Select(document => document["age"]))}
                            }))
                        .ToList<CosmosElement>()
                ),
                (
                    "SELECT c.name AS Name, MAKESET(c.age) AS AgeSet FROM c GROUP BY c.name",
                    documents
                        .GroupBy(document => document["name"])
                        .Select(grouping => CosmosObject.Create(
                            new Dictionary<string, CosmosElement>()
                            {
                                { "Name", grouping.Key },
                                { "AgeSet", CosmosArray.Create(grouping.Select(document => document["age"]).Distinct())}
                            }))
                        .ToList<CosmosElement>()
                )
            };

            // Test query correctness.
            foreach ((string query, List<CosmosElement> expectedResults) in queryAndExpectedResultsListArrayAggregates)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    QueryRequestOptions queryRequestOptions = new QueryRequestOptions()
                    {
                        MaxConcurrency = 2,
                        MaxItemCount = maxItemCount,
                        MaxBufferedItemCount = 100,
                    };

                    List<CosmosElement> actualWithoutContinuationTokens = await QueryTestsBase.QueryWithoutContinuationTokensAsync<CosmosElement>(
                        container,
                        query,
                        queryRequestOptions);
                    this.NormalizeGroupByArrayAggregateResults(actualWithoutContinuationTokens);
                    HashSet<CosmosElement> actualWithoutContinuationTokensSet = new HashSet<CosmosElement>(actualWithoutContinuationTokens);

                    List<CosmosElement> actualWithCombinations = await QueryTestsBase.RunQueryCombinationsAsync(
                        container,
                        query,
                        queryRequestOptions,
                        QueryDrainingMode.HoldState);
                    this.NormalizeGroupByArrayAggregateResults(actualWithCombinations);
                    HashSet<CosmosElement> actualWithCombinationsSet = new HashSet<CosmosElement>(actualWithCombinations);

                    Assert.IsTrue(
                       actualWithoutContinuationTokensSet.SetEquals(actualWithCombinationsSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"ActualWithoutContinuationTokens: {JsonConvert.SerializeObject(actualWithoutContinuationTokensSet)}" +
                       $"ActualWithCombinations: {JsonConvert.SerializeObject(actualWithCombinationsSet)}");

                    this.NormalizeGroupByArrayAggregateResults(expectedResults);
                    HashSet<CosmosElement> expectedSet = new HashSet<CosmosElement>(expectedResults);

                    Assert.IsTrue(
                       actualWithoutContinuationTokensSet.SetEquals(expectedSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual {JsonConvert.SerializeObject(actualWithoutContinuationTokensSet)}" +
                       $"Expected: {JsonConvert.SerializeObject(expectedSet)}");
                }
            }
        
            // Test that continuation token is blocked

            try
            {
                List<JToken> actual = await QueryTestsBase.QueryWithContinuationTokensAsync<JToken>(
                    container,
                    "SELECT c.age FROM c GROUP BY c.age",
                    new QueryRequestOptions()
                    {
                        MaxConcurrency = 2,
                        MaxItemCount = 1
                    });
                Assert.Fail("Expected an error when trying to drain a GROUP BY query with continuation tokens.");
            }
            catch (Exception)
            {
            }
        }

        private void NormalizeGroupByArrayAggregateResults(
            List<CosmosElement> results)
        {
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i] is not CosmosObject)
                {
                    Assert.Fail("This function assumes results are CosmosObjects (ie, not from a SELECT VALUE query).");
                }

                Dictionary<string, CosmosElement> normalizedResult = new Dictionary<string, CosmosElement>();

                CosmosObject resultObject = (CosmosObject)results[i];
                foreach (KeyValuePair<string, CosmosElement> kvp in resultObject)
                {
                    if (kvp.Value is CosmosArray cosmosArray)
                    {
                        CosmosElement[] normalizedArray = cosmosArray.ToArray();
                        Array.Sort(normalizedArray);
                        normalizedResult.Add(kvp.Key, CosmosArray.Create(normalizedArray));
                    }
                    else
                    {
                        normalizedResult.Add(kvp.Key, kvp.Value);
                    }
                }

                results[i] = CosmosObject.Create(normalizedResult);
            }
        }
    }
}
