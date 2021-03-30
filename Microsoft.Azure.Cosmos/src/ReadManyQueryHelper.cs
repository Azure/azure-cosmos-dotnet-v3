//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class ReadManyQueryHelper : ReadManyHelper
    {
        private readonly IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap;
        private readonly string partitionKeySelector;
        private readonly ITrace trace;
        private readonly int maxConcurrency = Environment.ProcessorCount * 10;
        private readonly ContainerCore container;
        
        public ReadManyQueryHelper(IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap,
                                   PartitionKeyDefinition partitionKeyDefinition,
                                   ContainerCore container,
                                   ITrace trace)
        {
            this.partitionKeyRangeItemMap = partitionKeyRangeItemMap;
            this.partitionKeySelector = this.CreatePkSelector(partitionKeyDefinition);
            this.container = container;
            this.trace = trace;
        }

        public async Task<ResponseMessage> ExecuteReadManyRequestAsync(CancellationToken cancellationToken = default)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(0, this.maxConcurrency);
            List<Task<List<byte[]>>> tasks = new List<Task<List<byte[]>>>();

            foreach (KeyValuePair<PartitionKeyRange, List<(string, PartitionKey)>> entry in this.partitionKeyRangeItemMap)
            {
                tasks.Add(Task.Run(async () =>
                {
                    // Only allow 'maxConcurrency' number of queries at a time
                    await semaphore.WaitAsync();

                    try
                    {
                        QueryDefinition queryDefinition = (this.partitionKeySelector == "[\"id\"]") ?
                                                   this.CreateReadManyQueryDefifnitionForId(entry.Value) :
                                                   this.CreateReadManyQueryDefifnitionForOther(entry.Value);

                        List<byte[]> pages = new List<byte[]>();
                        FeedIterator feedIterator = this.container.GetItemQueryStreamIterator(new FeedRangePartitionKeyRange(entry.Key.Id),
                                                                        queryDefinition);
                        while (feedIterator.HasMoreResults)
                        {
                            using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(cancellationToken))
                            {
                                pages.Add(Cosmos)
                            }
                            pages.Add();
                        }

                        return pages;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            // Restore semaphore to max Count and allow tasks to run
            semaphore.Release(this.maxConcurrency);

            List<ResponseMessage>[] queryResponses = await Task.WhenAll(tasks);

        }

        internal static ResponseMessage CombineMultipleStreams(List<ResponseMessage> responseMessages)
        {

        }

        private QueryDefinition CreateReadManyQueryDefifnitionForId(List<(string, PartitionKey)> items)
        {
            StringBuilder queryStringBuilder = new StringBuilder();

            queryStringBuilder.Append("SELECT * FROM c WHERE c.id IN ( ");
            for (int i = 0; i < items.Count; i++)
            {
                queryStringBuilder.Append($"'{items[i].Item1}'");
                if (i < items.Count - 1)
                {
                    queryStringBuilder.Append(",");
                }
            }
            queryStringBuilder.Append(" )");

            return new QueryDefinition(queryStringBuilder.ToString());
        }

        private QueryDefinition CreateReadManyQueryDefifnitionForOther(List<(string, PartitionKey)> items)
        {
            StringBuilder queryStringBuilder = new StringBuilder();
            SqlParameterCollection sqlParameters = new SqlParameterCollection();

            queryStringBuilder.Append("SELECT * FROM c WHERE ( ");
            for (int i = 0; i < items.Count; i++)
            {
                string pkParamName = "@param_pk" + i;
                sqlParameters.Add(new SqlParameter(pkParamName, items[i].Item2));

                string idParamName = "@param_id" + i;
                sqlParameters.Add(new SqlParameter(idParamName, items[i].Item2));

                queryStringBuilder.Append("( ");
                queryStringBuilder.Append("c.id = ");
                queryStringBuilder.Append(idParamName);
                queryStringBuilder.Append(" AND ");
                queryStringBuilder.Append("c");
                queryStringBuilder.Append(this.partitionKeySelector);
                queryStringBuilder.Append(" = ");
                queryStringBuilder.Append(pkParamName);
                queryStringBuilder.Append(" )");

                if (i < items.Count - 1)
                {
                    queryStringBuilder.Append(" OR ");
                }
            }
            queryStringBuilder.Append(" )");

            return QueryDefinition.CreateFromQuerySpec(new SqlQuerySpec(queryStringBuilder.ToString(),
                                                        sqlParameters));
        }

        private string CreatePkSelector(PartitionKeyDefinition partitionKeyDefinition)
        {
            List<string> pathParts = new List<string>();
            foreach (string path in partitionKeyDefinition.Paths)
            {
                // Ignore '/' in the beginning and escaping quote
                string modifiedString = path.Substring(1).Replace("\"", "\\");
                pathParts.Add(modifiedString);
            }

            return string.Join(String.Empty, pathParts);
        }
    }
}
