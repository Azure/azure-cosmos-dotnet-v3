namespace Microsoft.Azure.Cosmos.EmulatorTests.Query
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public sealed class QueryContinuationTokenBaselineTests : BaselineTests<QueryContinuationTokenBaselineTestInput, QueryContinuationTokenBaselineTestOutput>
    {
        public override QueryContinuationTokenBaselineTestOutput ExecuteTest(QueryContinuationTokenBaselineTestInput input)
        {
            FeedIterator<JToken> feedIterator = input.Container.GetItemQueryIterator<JToken>(
                new QueryDefinition(input.QueryText),
                continuationToken: null,
                new QueryRequestOptions()
                {
                    MaxItemCount = 10,
                });

            List<string> continuationTokens = new List<string>();
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<JToken> response = feedIterator.ReadNextAsync().Result;
                continuationTokens.Add(response.ContinuationToken);
            }

            return new QueryContinuationTokenBaselineTestOutput(continuationTokens.ToImmutableArray());
        }

        [TestMethod]
        public async Task RunBaselinesAsync()
        {
            CosmosClient client = TestCommon.CreateCosmosClient(useGateway: false);
            Database database = await client.CreateDatabaseAsync(Guid.NewGuid().ToString() + "db");
            ContainerResponse containerResponse = await database.CreateContainerAsync(
                new ContainerProperties
                {
                    Id = Guid.NewGuid().ToString() + "container",
                    IndexingPolicy = new Cosmos.IndexingPolicy
                    {
                        IncludedPaths = new Collection<Cosmos.IncludedPath>
                        {
                            new Cosmos.IncludedPath
                            {
                                Path = "/*",
                                Indexes = new Collection<Cosmos.Index>
                                {
                                    Cosmos.Index.Range(Cosmos.DataType.Number),
                                    Cosmos.Index.Range(Cosmos.DataType.String),
                                }
                            }
                        }
                    },
                    PartitionKey = new Microsoft.Azure.Documents.PartitionKeyDefinition
                    {
                        Paths = new Collection<string> { "/id" },
                        Kind = Microsoft.Azure.Documents.PartitionKind.Hash
                    }
                },
                throughput: 25000);
            Container container = containerResponse.Container;

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

            foreach (string document in documents)
            {
                _ = container.CreateItemAsync(JToken.Parse(document)).Result;
            }

            List<QueryContinuationTokenBaselineTestInput> inputs = new List<QueryContinuationTokenBaselineTestInput>()
            {
                new QueryContinuationTokenBaselineTestInput(description: "SELECT *", container: container, queryText: "SELECT * FROM c"),
                new QueryContinuationTokenBaselineTestInput(description: "AGRREGATES", container: container, queryText: "SELECT VALUE COUNT(1) FROM c"),
                //new QueryContinuationTokenBaselineTestInput(description: "DISTINCT", container: container, queryText: "SELECT DISTINCT c.name FROM c"),
                //new QueryContinuationTokenBaselineTestInput(description: "GROUP BY", container: container, queryText: "SELECT c.name FROM c GROUP BY c.name"),
                //new QueryContinuationTokenBaselineTestInput(description: "Multiple Aggregates", container: container, queryText: "SELECT MIN(c.age), MAX(c.age) FROM c"),
                new QueryContinuationTokenBaselineTestInput(description: "ORDER BY", container: container, queryText: "SELECT * FROM c ORDER BY c._ts"),
                new QueryContinuationTokenBaselineTestInput(description: "TOP", container: container, queryText: "SELECT TOP 10 * FROM c"),
                new QueryContinuationTokenBaselineTestInput(description: "OFFSET LIMIT", container: container, queryText: "SELECT * FROM c OFFSET 20 LIMIT 20"),
            };

            this.ExecuteTestSuite(inputs);

            await database.DeleteAsync();
        }
    }

    public sealed class QueryContinuationTokenBaselineTestInput : BaselineTestInput
    {
        internal QueryContinuationTokenBaselineTestInput(string description, Container container, string queryText)
            : base(description)
        {
            this.Container = container;
            this.QueryText = queryText ?? throw new ArgumentNullException(nameof(queryText));
        }

        public Container Container { get; }

        public string QueryText { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteElementString(nameof(this.QueryText), this.QueryText);
        }
    }

    public sealed class QueryContinuationTokenBaselineTestOutput : BaselineTestOutput
    {
        public QueryContinuationTokenBaselineTestOutput(ImmutableArray<string> continuationTokens)
        {
            this.ContinuationTokens = continuationTokens;
        }

        public ImmutableArray<string> ContinuationTokens { get; }

        public override void SerializeAsXml(XmlWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(nameof(this.ContinuationTokens));

            foreach (string continuationToken in this.ContinuationTokens)
            {
                xmlWriter.WriteElementString(nameof(continuationToken), continuationToken);

            }

            xmlWriter.WriteEndElement();
        }
    }
}
