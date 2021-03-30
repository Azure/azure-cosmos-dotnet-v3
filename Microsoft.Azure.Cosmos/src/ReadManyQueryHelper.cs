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
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class ReadManyQueryHelper : ReadManyHelper
    {
        private readonly string partitionKeySelector;
        private readonly int maxConcurrency = Environment.ProcessorCount * 10;
        private readonly ContainerCore container;
        
        public ReadManyQueryHelper(PartitionKeyDefinition partitionKeyDefinition,
                                   ContainerCore container)
        {
            this.partitionKeySelector = this.CreatePkSelector(partitionKeyDefinition);
            this.container = container;
        }

        public override async Task<ResponseMessage> ExecuteReadManyRequestAsync(IReadOnlyList<(string, PartitionKey)> items,
                                                                                ITrace trace, 
                                                                                CancellationToken cancellationToken = default)
        {
            IDictionary<string, List<(string, PartitionKey)>> partitionKeyRangeItemMap =
                                await this.CreatePartitionKeyRangeItemListMapAsync(items, cancellationToken);

            SemaphoreSlim semaphore = new SemaphoreSlim(0, this.maxConcurrency);
            List<Task<List<ResponseMessage>>> tasks = new List<Task<List<ResponseMessage>>>();

            foreach (KeyValuePair<string, List<(string, PartitionKey)>> entry in partitionKeyRangeItemMap)
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

                        List<ResponseMessage> pages = new List<ResponseMessage>();
                        FeedIteratorInternal feedIterator = (FeedIteratorInternal)this.container.GetItemQueryStreamIterator(new FeedRangePartitionKeyRange(entry.Key),
                                                                        queryDefinition);
                        while (feedIterator.HasMoreResults)
                        {
                            using (ResponseMessage responseMessage = await feedIterator.ReadNextAsync(trace, cancellationToken))
                            {
                                pages.Add(responseMessage);
                            }
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
            return this.CombineStreamsFromQueryResponses(queryResponses);
        }

        public override Task<FeedResponse<T>> ExecuteReadManyRequestAsync<T>(IReadOnlyList<(string, PartitionKey)> items,
                                                                            ITrace trace,
                                                                            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        private async Task<IDictionary<string, List<(string, PartitionKey)>>> CreatePartitionKeyRangeItemListMapAsync(
            IReadOnlyList<(string, PartitionKey)> items,
            CancellationToken cancellationToken = default)
        {
            CollectionRoutingMap collectionRoutingMap = await this.container.GetRoutingMapAsync(cancellationToken);
            PartitionKeyDefinition partitionKeyDefinition = await this.container.GetPartitionKeyDefinitionAsync(cancellationToken);

            IDictionary<string, List<(string, PartitionKey)>> partitionKeyRangeItemMap = new
                Dictionary<string, List<(string, PartitionKey)>>();

            foreach ((string id, PartitionKey pk) item in items)
            {
                string effectivePartitionKeyValue = item.pk.InternalKey.GetEffectivePartitionKeyString(partitionKeyDefinition);
                PartitionKeyRange partitionKeyRange = collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyValue);
                if (partitionKeyRangeItemMap.TryGetValue(partitionKeyRange.Id, out List<(string, PartitionKey)> itemList))
                {
                    itemList.Add(item);
                }
                else
                {
                    List<(string, PartitionKey)> newList = new List<(string, PartitionKey)> { item };
                    partitionKeyRangeItemMap[partitionKeyRange.Id] = newList;
                }
            }

            return partitionKeyRangeItemMap;
        }

        private ResponseMessage CombineStreamsFromQueryResponses(List<ResponseMessage>[] queryResponses)
        {
            List<CosmosElement> cosmosElements = new List<CosmosElement>();
            foreach (List<ResponseMessage> responseMessagesForSinglePartition in queryResponses)
            {
                foreach (ResponseMessage responseMessage in responseMessagesForSinglePartition)
                {
                    if (responseMessage is QueryResponse queryResponse) 
                    {
                        cosmosElements.AddRange(queryResponse.CosmosElements);
                    }
                    else
                    {
                        throw new InvalidOperationException("Read Many is being used with Query");
                    }
                }
            }

            return new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = CosmosElementSerializer.ToStream(string.Empty, cosmosElements, ResourceType.Document)
            };
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
