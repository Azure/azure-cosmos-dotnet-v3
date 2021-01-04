namespace Microsoft.Azure.Cosmos.Tests.Query.EndToEndTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.Query.Core.Pipeline;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tests.Poco;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;

    [TestClass]
    public abstract partial class EndToEndTestsBase
    {
        private delegate Task ImplementationAsync<T>(
            IQueryableContainer documentContainer,
            IReadOnlyList<CosmosObject> documentsInserted,
            T testArgument);

        private delegate Task ImplementationAsync(
            IQueryableContainer documentContainer,
            IReadOnlyList<CosmosObject> documentsInserted);

        private async Task RunQueryTestAsync(
            IReadOnlyList<CosmosObject> documentsToInsert,
            ImplementationAsync implementationAsync)
        {
            (IQueryableContainer container, List<CosmosObject> documentsInserted) = await this.CreateContainerAsync(
                documentsToInsert);

            using (container)
            {
                await implementationAsync(container, documentsInserted);
            }
        }

        private async Task RunQueryTestAsync<T>(
            IReadOnlyList<CosmosObject> documentsToInsert,
            ImplementationAsync<T> implementationAsync,
            T testArgument)
        {
            (IQueryableContainer container, List<CosmosObject> documentsInserted) = await this.CreateContainerAsync(
                documentsToInsert);

            using (container)
            {
                await implementationAsync(container, documentsInserted, testArgument);
            }
        }

        private static List<CosmosObject> GenerateRandomDocuments(int numberOfDocuments)
        {
            List<CosmosObject> documents = new List<CosmosObject>();

            for (int i = 0; i < numberOfDocuments; i++)
            {
                Person person = Person.GetRandomPerson();
                string serializedPerson = JsonConvert.SerializeObject(person);
                CosmosObject document = CosmosObject.Parse(serializedPerson);
                documents.Add(document);
            }

            return documents;
        }

        private static Task<List<CosmosElement>> ValidateQueryAsync(
            IQueryableContainer container,
            string query,
            int pageSize = 10,
            int maxConcurrency = 10)
        {
            return ValidateQueryCombinationsAsync(
                container,
                query,
                QueryDrainingMode.ClientWithoutState | QueryDrainingMode.ClientWithState | QueryDrainingMode.ComputeWithoutState | QueryDrainingMode.ComputeWithState,
                pageSize,
                maxConcurrency);
        }

        [Flags]
        public enum QueryDrainingMode
        {
            None = 0,
            ClientWithoutState = 1 << 0,
            ClientWithState = 1 << 1,
            ComputeWithoutState = 1 << 2,
            ComputeWithState = 1 << 3,
        }

        private static async Task<List<CosmosElement>> ValidateQueryCombinationsAsync(
            IQueryableContainer container,
            string query,
            QueryDrainingMode queryDrainingMode,
            int pageSize = 10,
            int maxConcurrency = 10)
        {
            if (queryDrainingMode == QueryDrainingMode.None)
            {
                throw new ArgumentOutOfRangeException(nameof(queryDrainingMode));
            }

            Dictionary<QueryDrainingMode, List<CosmosElement>> queryExecutionResults = new Dictionary<QueryDrainingMode, List<CosmosElement>>();

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ClientWithoutState))
            {
                List<CosmosElement> clientWithoutStateResults = await DrainQueryAsync(
                    container,
                    query,
                    useState: false,
                    executionEnvironment: ExecutionEnvironment.Client,
                    pageSize,
                    maxConcurrency);

                queryExecutionResults[QueryDrainingMode.ClientWithoutState] = clientWithoutStateResults;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ClientWithState))
            {
                List<CosmosElement> clientWithStateResults = await DrainQueryAsync(
                    container,
                    query,
                    useState: true,
                    executionEnvironment: ExecutionEnvironment.Client,
                    pageSize,
                    maxConcurrency);

                queryExecutionResults[QueryDrainingMode.ClientWithState] = clientWithStateResults;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ComputeWithoutState))
            {
                List<CosmosElement> computeWithoutStateResults = await DrainQueryAsync(
                    container,
                    query,
                    useState: false,
                    executionEnvironment: ExecutionEnvironment.Compute,
                    pageSize,
                    maxConcurrency);

                queryExecutionResults[QueryDrainingMode.ComputeWithoutState] = computeWithoutStateResults;
            }

            if (queryDrainingMode.HasFlag(QueryDrainingMode.ComputeWithState))
            {
                List<CosmosElement> computeWithStateResults = await DrainQueryAsync(
                    container,
                    query,
                    useState: true,
                    executionEnvironment: ExecutionEnvironment.Compute,
                    pageSize,
                    maxConcurrency);

                queryExecutionResults[QueryDrainingMode.ComputeWithState] = computeWithStateResults;
            }

            foreach (QueryDrainingMode queryDrainingMode1 in queryExecutionResults.Keys)
            {
                foreach (QueryDrainingMode queryDrainingMode2 in queryExecutionResults.Keys)
                {
                    if (queryDrainingMode1 != queryDrainingMode2)
                    {
                        List<CosmosElement> first = queryExecutionResults[queryDrainingMode1];
                        List<CosmosElement> second = queryExecutionResults[queryDrainingMode2];
                        Assert.IsTrue(
                            first.SequenceEqual(second),
                            $"{query} returned different results.\n" +
                            $"{queryDrainingMode1}: {JsonConvert.SerializeObject(first)}\n" +
                            $"{queryDrainingMode2}: {JsonConvert.SerializeObject(second)}\n");
                    }
                }
            }

            Dictionary<string, PartitionKeyRange> ridToPartitionKeyRange = await container.GetRidToPartitionKeyRangeAsync();
            List<CosmosElement> documents = await container.GetDataSourceAsync();

            SqlQuery parsedQuery = SqlQueryParser.Parse(query);

            IEnumerable<CosmosElement> oracleResults = SqlInterpreter.ExecuteQuery(
                documents,
                parsedQuery,
                ridToPartitionKeyRange);

            List<CosmosElement> actualResults = queryExecutionResults.Values.First();

            Assert.IsTrue(
                actualResults.SequenceEqual(oracleResults),
                $"{query} returned different results.\n" +
                $"Oracle: {JsonConvert.SerializeObject(oracleResults)}\n" +
                $"Actual: {JsonConvert.SerializeObject(actualResults)}\n");

            return actualResults;
        }

        private static async Task<List<CosmosElement>> DrainQueryAsync(
            IQueryableContainer container,
            string query,
            bool useState,
            ExecutionEnvironment executionEnvironment,
            int pageSize = 10,
            int maxConcurrency = 10)
        {
            TryCatch<IQueryPipeline> monadicQueryPipelineStage = container.MonadicCreateQueryPipeline(
                query,
                pageSize,
                maxConcurrency,
                executionEnvironment,
                requestContinuationToken: null);

            monadicQueryPipelineStage.ThrowIfFailed();

            List<CosmosElement> results = new List<CosmosElement>();

            IQueryPipeline queryPipelineStage = monadicQueryPipelineStage.Result;
            await foreach (TryCatch<QueryPage> monadicQueryPage in queryPipelineStage)
            {
                if (monadicQueryPage.Failed)
                {
                    if (monadicQueryPage.InnerMostException is CosmosException cosmosException
                        && cosmosException.StatusCode == (HttpStatusCode)429)
                    {
                        // Just do a blind retry on 429s
                        continue;
                    }

                    monadicQueryPage.ThrowIfFailed();
                }

                QueryPage queryPage = monadicQueryPage.Result;
                Assert.IsTrue(queryPage.Documents.Count <= pageSize);

                results.AddRange(queryPage.Documents);

                if (useState && (queryPage.State != null))
                {
                    TryCatch<IQueryPipeline> monadicQueryPipelineStageWithState = container.MonadicCreateQueryPipeline(
                        query,
                        pageSize,
                        maxConcurrency,
                        executionEnvironment,
                        requestContinuationToken: queryPage.State.Value);

                    monadicQueryPipelineStageWithState.ThrowIfFailed();

                    queryPipelineStage = monadicQueryPipelineStageWithState.Result;
                }
            }

            return results;
        }

        internal abstract Task<(IQueryableContainer, List<CosmosObject>)> CreateContainerAsync(
            IReadOnlyList<CosmosObject> documentsToInsert);

        internal interface IQueryableContainer : IDisposable
        {
            TryCatch<IQueryPipeline> MonadicCreateQueryPipeline(
                string queryText,
                int pageSize,
                int maxConcurrency,
                ExecutionEnvironment executionEnvironment,
                CosmosElement requestContinuationToken);

            Task<Dictionary<string, Microsoft.Azure.Documents.PartitionKeyRange>> GetRidToPartitionKeyRangeAsync();

            Task<List<CosmosElement>> GetDataSourceAsync();
        }

        internal interface IQueryPipeline : IAsyncEnumerable<TryCatch<QueryPage>>
        {
        }
    }
}
