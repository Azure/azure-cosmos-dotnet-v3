namespace Microsoft.Azure.Cosmos.EmulatorTests.Tracing
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Xml;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Services.Management.Tests.BaselineTest;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public sealed class EndToEndTraceWriterBaselineTests : BaselineTests<EndToEndTraceWriterBaselineTests.Input, EndToEndTraceWriterBaselineTests.Output>
    {
        [TestMethod]
        public async Task ScenariosAsync()
        {
            List<Input> inputs = new List<Input>();

            CosmosClient client = Microsoft.Azure.Cosmos.SDK.EmulatorTests.TestCommon.CreateCosmosClient(useGateway: false);
            Database database = (await client.CreateDatabaseAsync(
                Guid.NewGuid().ToString(),
                cancellationToken: default)).Database;
            Container container = (await database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: 20000)).Container;

            for (int i = 0; i < 100; i++)
            {
                CosmosObject cosmosObject = CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "id", CosmosString.Create(i.ToString()) }
                    });

                _ = await container.CreateItemAsync(JToken.Parse(cosmosObject.ToString()));
            }

            int startLineNumber;
            int endLineNumber;

            //----------------------------------------------------------------
            //  ReadFeed
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                FeedIteratorInternal feedIterator = (FeedIteratorInternal)container.GetItemQueryStreamIterator(
                    queryText: null);

                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        await feedIterator.ReadNextAsync(rootTrace, cancellationToken: default);
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("ReadFeed", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  ChangeFeed
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                ContainerInternal containerInternal = (ContainerInternal)container;
                FeedIteratorInternal feedIterator = (FeedIteratorInternal)containerInternal.GetChangeFeedStreamIterator(
                    ChangeFeedStartFrom.Beginning(),
                    ChangeFeedMode.Incremental);

                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        ResponseMessage responseMessage = await feedIterator.ReadNextAsync(rootTrace, cancellationToken: default);
                        if (responseMessage.StatusCode == System.Net.HttpStatusCode.NotModified)
                        {
                            break;
                        }
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("ChangeFeed", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Query
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                FeedIteratorInternal feedIterator = (FeedIteratorInternal)container.GetItemQueryStreamIterator(
                    queryText: "SELECT * FROM c");

                TraceForBaselineTesting rootTrace;
                using (rootTrace = TraceForBaselineTesting.GetRootTrace())
                {
                    while (feedIterator.HasMoreResults)
                    {
                        await feedIterator.ReadNextAsync(rootTrace, cancellationToken: default);
                    }
                }
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Query", rootTrace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Point Read
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                ItemResponse<JToken> itemResponse = await container.ReadItemAsync<JToken>(
                    id: "0",
                    partitionKey: new Cosmos.PartitionKey("0"));

                ITrace trace = ((CosmosTraceDiagnostics)itemResponse.Diagnostics).Value;
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Point Read", trace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            //----------------------------------------------------------------
            //  Point Write
            //----------------------------------------------------------------
            {
                startLineNumber = GetLineNumber();
                CosmosObject cosmosObject = CosmosObject.Create(
                    new Dictionary<string, CosmosElement>()
                    {
                        { "id", CosmosString.Create(9001.ToString()) }
                    });

                ItemResponse<JToken> itemResponse = await container.CreateItemAsync(JToken.Parse(cosmosObject.ToString()));

                ITrace trace = ((CosmosTraceDiagnostics)itemResponse.Diagnostics).Value;
                endLineNumber = GetLineNumber();

                inputs.Add(new Input("Point Write", trace, startLineNumber, endLineNumber));
            }
            //----------------------------------------------------------------

            await database.DeleteAsync();

            this.ExecuteTestSuite(inputs);
        }

        public override Output ExecuteTest(Input input)
        {
            string text = TraceWriter.TraceToText(input.Trace);
            string json = TraceWriter.TraceToJson(input.Trace);

            return new Output(text, JToken.Parse(json).ToString(Newtonsoft.Json.Formatting.Indented));
        }

        private static int GetLineNumber([CallerLineNumber] int lineNumber = 0)
        {
            return lineNumber;
        }

        public sealed class Input : BaselineTestInput
        {
            private static readonly string[] sourceCode = File.ReadAllLines($"Tracing\\{nameof(EndToEndTraceWriterBaselineTests)}.cs");

            internal Input(string description, ITrace trace, int startLineNumber, int endLineNumber)
                : base(description)
            {
                this.Trace = trace ?? throw new ArgumentNullException(nameof(trace));
                this.StartLineNumber = startLineNumber;
                this.EndLineNumber = endLineNumber;
            }

            internal ITrace Trace { get; }

            public int StartLineNumber { get; }

            public int EndLineNumber { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteElementString(nameof(this.Description), this.Description);
                xmlWriter.WriteStartElement("Setup");
                ArraySegment<string> codeSnippet = new ArraySegment<string>(
                    sourceCode,
                    this.StartLineNumber,
                    this.EndLineNumber - this.StartLineNumber - 1);

                string setup;
                try
                {
                    setup =
                    Environment.NewLine
                    + string
                        .Join(
                            Environment.NewLine,
                            codeSnippet
                                .Select(x => x != string.Empty ? x.Substring("            ".Length) : string.Empty))
                    + Environment.NewLine;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                xmlWriter.WriteCData(setup ?? "asdf");
                xmlWriter.WriteEndElement();
            }
        }

        public sealed class Output : BaselineTestOutput
        {
            public Output(string text, string json)
            {
                this.Text = text ?? throw new ArgumentNullException(nameof(text));
                this.Json = json ?? throw new ArgumentNullException(nameof(json));
            }

            public string Text { get; }

            public string Json { get; }

            public override void SerializeAsXml(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement(nameof(this.Text));
                xmlWriter.WriteCData(this.Text);
                xmlWriter.WriteEndElement();

                xmlWriter.WriteStartElement(nameof(this.Json));
                xmlWriter.WriteCData(this.Json);
                xmlWriter.WriteEndElement();
            }
        }

        private sealed class TraceForBaselineTesting : ITrace
        {
            private readonly Dictionary<string, object> data;
            private readonly List<TraceForBaselineTesting> children;

            public TraceForBaselineTesting(
                string name,
                TraceLevel level,
                TraceComponent component,
                TraceForBaselineTesting parent)
            {
                this.Name = name ?? throw new ArgumentNullException(nameof(name));
                this.Level = level;
                this.Component = component;
                this.Parent = parent;
                this.children = new List<TraceForBaselineTesting>();
                this.data = new Dictionary<string, object>();
            }

            public string Name { get; }

            public Guid Id => Guid.Empty;

            public CallerInfo CallerInfo => new CallerInfo("MemberName", "FilePath", 42);

            public DateTime StartTime => DateTime.MinValue;

            public TimeSpan Duration => TimeSpan.Zero;

            public TraceLevel Level { get; }

            public TraceComponent Component { get; }

            public ITrace Parent { get; }

            public IReadOnlyList<ITrace> Children => this.children;

            public IReadOnlyDictionary<string, object> Data => this.data;

            public void AddDatum(string key, TraceDatum traceDatum)
            {
                this.data[key] = traceDatum;
            }

            public void AddDatum(string key, object value)
            {
                this.data[key] = value;
            }

            public void Dispose()
            {
            }

            public ITrace StartChild(string name, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                return this.StartChild(name, TraceComponent.Unknown, TraceLevel.Info, memberName, sourceFilePath, sourceLineNumber);
            }

            public ITrace StartChild(string name, TraceComponent component, TraceLevel level, [CallerMemberName] string memberName = "", [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            {
                TraceForBaselineTesting child = new TraceForBaselineTesting(name, level, component, parent: this);
                this.children.Add(child);
                return child;
            }

            public static TraceForBaselineTesting GetRootTrace()
            {
                return new TraceForBaselineTesting("Trace For Baseline Testing", TraceLevel.Info, TraceComponent.Unknown, parent: null);
            }
        }
    }
}
