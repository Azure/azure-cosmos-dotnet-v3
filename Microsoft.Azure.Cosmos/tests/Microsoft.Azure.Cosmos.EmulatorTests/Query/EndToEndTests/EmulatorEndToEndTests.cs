namespace Microsoft.Azure.Cosmos.EmulatorTests.Query.EndToEndTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.SDK.EmulatorTests;
    using Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests;
    using Microsoft.IdentityModel.Tokens;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [VisualStudio.TestTools.UnitTesting.TestClass]
    public sealed class EmulatorEndToEndTests : EndToEndTestsBase
    {
        private static readonly CosmosClient client = TestCommon.CreateCosmosClient(false);
        private static Cosmos.Database database;

        [ClassInitialize]

        public static void ClassSetup(TestContext testContext = null)
        {
            database = client.CreateDatabaseAsync(id: Guid.NewGuid().ToString()).Result;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            database.DeleteAsync().Wait();
        }

        internal override async Task<(IQueryableContainer, List<CosmosObject>)> CreateContainerAsync(
            IReadOnlyList<CosmosObject> documentsToInsert)
        {
            Container container = await database.CreateContainerAsync(
                id: Guid.NewGuid().ToString(),
                partitionKeyPath: "/id",
                throughput: 20000);

            List<CosmosObject> insertedDocuments = new List<CosmosObject>();
            foreach (CosmosObject document in documentsToInsert)
            {
                CosmosObject documentWithId;
                if (!document.ContainsKey("id"))
                {
                    Dictionary<string, CosmosElement> keyValuePairs = new Dictionary<string, CosmosElement>()
                    {
                        { "id", CosmosString.Create(Guid.NewGuid().ToString()) }
                    };

                    foreach (KeyValuePair<string, CosmosElement> kvp in document)
                    {
                        keyValuePairs.Add(kvp.Key, kvp.Value);
                    }

                    documentWithId = CosmosObject.Create(keyValuePairs);
                }
                else
                {
                    documentWithId = document;
                }

                JToken insertedDocument = await container.CreateItemAsync(JToken.Parse(documentWithId.ToString()));
                insertedDocuments.Add(CosmosObject.Parse(insertedDocument.ToString()));
            }

            return (new EmulatorQueryableContainer(container), insertedDocuments);
        }

        private sealed class EmulatorQueryableContainer : IQueryableContainer
        {
            private readonly Container container;

            public EmulatorQueryableContainer(Container container)
            {
                this.container = container ?? throw new ArgumentNullException(nameof(container));
            }

            public void Dispose()
            {
                this.container.DeleteContainerAsync().Wait();
            }

            public TryCatch<IQueryPipeline> MonadicCreateQueryPipeline(
                string queryText,
                int pageSize,
                int maxConcurrency,
                ExecutionEnvironment executionEnvironment,
                CosmosElement requestContinuationToken)
            {
                EmulatorQueryPipeline queryPipeline = new EmulatorQueryPipeline(
                    this.container,
                    queryText,
                    pageSize,
                    maxConcurrency,
                    executionEnvironment,
                    requestContinuationToken);
                return TryCatch<IQueryPipeline>.FromResult(queryPipeline);
            }

            private sealed class EmulatorQueryPipeline : IQueryPipeline
            {
                private readonly Container container;
                private readonly string queryText;
                private readonly int pageSize;
                private readonly int maxConcurrency;
                private readonly ExecutionEnvironment executionEnvironment;
                private readonly CosmosElement requestContinuationToken;

                public EmulatorQueryPipeline(Container container,
                    string queryText,
                    int pageSize,
                    int maxConcurrency,
                    ExecutionEnvironment executionEnvironment,
                    CosmosElement requestContinuationToken)
                {
                    this.container = container;
                    this.queryText = queryText;
                    this.pageSize = pageSize;
                    this.maxConcurrency = maxConcurrency;
                    this.executionEnvironment = executionEnvironment;
                    this.requestContinuationToken = requestContinuationToken;
                }

                public IAsyncEnumerator<TryCatch<QueryPage>> GetAsyncEnumerator(
                    CancellationToken cancellationToken = default)
                {
                    static async IAsyncEnumerable<TryCatch<QueryPage>> CreateEnumerable(
                        Container container,
                        string queryText,
                        int pageSize,
                        int maxConcurrency,
                        ExecutionEnvironment executionEnvironment,
                        CosmosElement requestContinuationToken)
                    {
                        FeedIterator<CosmosElement> feedIterator = container.GetItemQueryIterator<CosmosElement>(
                            queryText,
                            requestContinuationToken is CosmosString tokenAsString ? tokenAsString.Value : null,
                            new QueryRequestOptions()
                            {
                                MaxItemCount = pageSize,
                                MaxConcurrency = maxConcurrency,
                                ExecutionEnvironment = executionEnvironment
                            });

                        while (feedIterator.HasMoreResults)
                        {
                            FeedResponse<CosmosElement> page = await feedIterator.ReadNextAsync();
                            QueryPage queryPage = new QueryPage(
                                page.ToList(), 
                                page.RequestCharge,
                                page.ActivityId,
                                responseLengthInBytes: 1337,
                                cosmosQueryExecutionInfo: default,
                                disallowContinuationTokenMessage: null,
                                state: page.ContinuationToken != null ? new QueryState(CosmosElement.Parse(page.ContinuationToken)) : null);
                            yield return TryCatch<QueryPage>.FromResult(queryPage);
                        }
                    }

                    return CreateEnumerable(
                        this.container,
                        this.queryText,
                        this.pageSize,
                        this.maxConcurrency,
                        this.executionEnvironment,
                        this.requestContinuationToken)
                        .GetAsyncEnumerator();
                }
            }
        }
    }
}