namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class GroupByCrossPartitionQueryTests : CrossPartitionQueryTestsBase
    {
        [TestMethod]
        public async Task TestGroupByQuery()
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

            await this.CreateIngestQueryDelete(
                ConnectionModes.Direct,
                CollectionTypes.MultiPartition,
                documents,
                this.TestGroupByQueryHelper);
        }

        private async Task TestGroupByQueryHelper(
            Container container,
            IEnumerable<Document> documents)
        {
            IEnumerable<JToken> documentsAsJTokens = documents.Select(document => JToken.FromObject(document));
            List<Tuple<string, IEnumerable<JToken>>> queryAndExpectedResultsList = new List<Tuple<string, IEnumerable<JToken>>>()
            {
                // ------------------------------------------
                // Simple property reference
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age FROM c GROUP BY c.age",
                    documentsAsJTokens
                        .GroupBy(document => document["age"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("age", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value).
                        Select(grouping => new JObject(new JProperty("name", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.team FROM c GROUP BY c.team",
                    documentsAsJTokens
                        .GroupBy(document => document["team"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("team", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.gender FROM c GROUP BY c.gender",
                    documentsAsJTokens
                        .GroupBy(document => document["gender"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("gender", grouping.Key)))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.id FROM c GROUP BY c.id",
                    documentsAsJTokens
                        .GroupBy(document => document["id"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(new JProperty("id", grouping.Key)))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age, c.name FROM c GROUP BY c.age, c.name",
                    documentsAsJTokens
                        .GroupBy(document => new JObject(
                            new JProperty("age", document["age"]),
                            new JProperty("name", document["name"])),
                            JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("age", grouping.Key["age"]),
                            new JProperty("name", grouping.Key["name"])))),

                 // ------------------------------------------
                 // With Aggregates
                 // ------------------------------------------

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.age, COUNT(1) as count FROM c GROUP BY c.age",
                    documentsAsJTokens
                        .GroupBy(document => document["age"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("age", grouping.Key),
                            new JProperty("count", grouping.Count())))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, MIN(c.age) AS min_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("min_age", grouping.Select(document => document["age"]).Min(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, MAX(c.age) AS max_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("max_age", grouping.Select(document => document["age"]).Max(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, SUM(c.age) AS sum_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("sum_age", grouping.Select(document => document["age"]).Sum(jToken => jToken.Value<double>()))))),

                 new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, AVG(c.age) AS avg_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("avg_age", grouping.Select(document => document["age"]).Average(jToken => jToken.Value<double>()))))),

                  new Tuple<string, IEnumerable<JToken>>(
                    "SELECT c.name, Count(1) AS count, Min(c.age) AS min_age, Max(c.age) AS max_age FROM c GROUP BY c.name",
                    documentsAsJTokens
                        .GroupBy(document => document["name"], JsonTokenEqualityComparer.Value)
                        .Select(grouping => new JObject(
                            new JProperty("name", grouping.Key),
                            new JProperty("count", grouping.Count()),
                            new JProperty("min_age", grouping.Select(document => document["age"]).Min(jToken => jToken.Value<double>())),
                            new JProperty("max_age", grouping.Select(document => document["age"]).Max(jToken => jToken.Value<double>()))))),

                // ------------------------------------------
                // SELECT VALUE
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                        "SELECT VALUE c.age FROM c GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], JsonTokenEqualityComparer.Value)
                            .Select(grouping => grouping.Key)),

                // ------------------------------------------
                // Corner Cases
                // ------------------------------------------

                new Tuple<string, IEnumerable<JToken>>(
                    "SELECT AVG(\"asdf\") as avg_asdf FROM c GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject())),

                new Tuple<string, IEnumerable<JToken>>(
                    @"SELECT 
                        c.age, 
                        AVG(c.doesNotExist) as undefined_avg,
                        MIN(c.doesNotExist) as undefined_min,
                        MAX(c.doesNotExist) as undefined_max,
                        COUNT(c.doesNotExist) as undefined_count,
                        SUM(c.doesNotExist) as undefined_sum
                    FROM c 
                    GROUP BY c.age",
                        documentsAsJTokens
                            .GroupBy(document => document["age"], JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject(
                                new JProperty("age", grouping.Key),
                                // sum and count default the counter at 0
                                new JProperty("undefined_sum", 0),
                                new JProperty("undefined_count", 0)))),

                new Tuple<string, IEnumerable<JToken>>(
                    @"SELECT 
                        c.age, 
                        c.doesNotExist
                    FROM c 
                    GROUP BY c.age, c.doesNotExist",
                        documentsAsJTokens
                            .GroupBy(document => new JObject(
                                new JProperty("age", document["age"]),
                                new JProperty("doesNotExist", document["doesNotExist"])),
                                JsonTokenEqualityComparer.Value)
                            .Select(grouping => new JObject(
                                new JProperty("age", grouping.Key["age"])))),
            };

            // Test query correctness.
            foreach ((string query, IEnumerable<JToken> expectedResults) in queryAndExpectedResultsList)
            {
                foreach (int maxItemCount in new int[] { 1, 5, 10 })
                {
                    List<JToken> actualWithoutContinuationTokens = await CrossPartitionQueryTestsBase.QueryWithoutContinuationTokensAsync<JToken>(
                        container,
                        query,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 2,
                            MaxItemCount = maxItemCount,
                            MaxBufferedItemCount = 100,
                        });
                    HashSet<JToken> actualWithoutContinuationTokensSet = new HashSet<JToken>(actualWithoutContinuationTokens, JsonTokenEqualityComparer.Value);

                    List<JToken> actualWithTryGetContinuationTokens = await CrossPartitionQueryTestsBase.QueryWithCosmosElementContinuationTokenAsync<JToken>(
                        container,
                        query,
                        new QueryRequestOptions()
                        {
                            MaxConcurrency = 2,
                            MaxItemCount = maxItemCount,
                            MaxBufferedItemCount = 100,
                        });
                    HashSet<JToken> actualWithTryGetContinuationTokensSet = new HashSet<JToken>(actualWithTryGetContinuationTokens, JsonTokenEqualityComparer.Value);

                    Assert.IsTrue(
                       actualWithoutContinuationTokensSet.SetEquals(actualWithTryGetContinuationTokensSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"ActualWithoutContinuationTokens: {JsonConvert.SerializeObject(actualWithoutContinuationTokensSet)}" +
                       $"ActualWithTryGetContinuationTokens: {JsonConvert.SerializeObject(actualWithTryGetContinuationTokensSet)}");

                    List<JToken> expected = expectedResults.ToList();
                    HashSet<JToken> expectedSet = new HashSet<JToken>(expected, JsonTokenEqualityComparer.Value);

                    Assert.IsTrue(
                       actualWithoutContinuationTokensSet.SetEquals(expectedSet),
                       $"Results did not match for query: {query} with maxItemCount: {maxItemCount}" +
                       $"Actual {JsonConvert.SerializeObject(actualWithoutContinuationTokensSet)}" +
                       $"Expected: {JsonConvert.SerializeObject(expectedSet)}");
                }
            }

            // Test that continuation token is blocked
            {
                try
                {
                    List<JToken> actual = await CrossPartitionQueryTestsBase.QueryWithContinuationTokensAsync<JToken>(
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
        }
    }
}
