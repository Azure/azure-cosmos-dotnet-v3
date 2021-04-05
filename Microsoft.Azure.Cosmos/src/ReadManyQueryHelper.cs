//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal sealed class ReadManyQueryHelper : ReadManyHelper
    {
        private readonly List<string> partitionKeySelectors;
        private readonly PartitionKeyDefinition partitionKeyDefinition;
        private readonly int maxConcurrency = Environment.ProcessorCount * 10;
        private readonly ContainerCore container;
        private readonly ReadManyRequestOptions readManyRequestOptions;
        private readonly CancellationToken cancellationToken;

        public ReadManyQueryHelper(PartitionKeyDefinition partitionKeyDefinition,
                                   ContainerCore container,
                                   ReadManyRequestOptions readManyRequestOptions,
                                   CancellationToken cancellationToken)
        {
            this.partitionKeyDefinition = partitionKeyDefinition;
            this.partitionKeySelectors = this.CreatePkSelectors(partitionKeyDefinition);
            this.container = container;
            this.readManyRequestOptions = readManyRequestOptions;
            this.cancellationToken = cancellationToken;
        }

        public override async Task<ResponseMessage> ExecuteReadManyRequestAsync(IReadOnlyList<(string, PartitionKey)> items,
                                                                                ITrace trace)
        {
            IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap =
                                await this.CreatePartitionKeyRangeItemListMapAsync(items, this.cancellationToken);

            List<ResponseMessage>[] queryResponses = await this.ReadManyTaskHelperAsync<ResponseMessage>(partitionKeyRangeItemMap,
                                                                this.GenerateStreamResponsesForPartitionAsync,
                                                                trace);

            ContainerProperties containerProperties = await this.container.GetCachedContainerPropertiesAsync(forceRefresh: false,
                                                                                           trace: trace,
                                                                                           cancellationToken: this.cancellationToken);

            return this.CombineStreamsFromQueryResponses(queryResponses, containerProperties.ResourceId, trace); // also disposes the response messages
        }

        public override async Task<FeedResponse<T>> ExecuteReadManyRequestAsync<T>(IReadOnlyList<(string, PartitionKey)> items,
                                                                            ITrace trace)
        {
            IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap =
                                await this.CreatePartitionKeyRangeItemListMapAsync(items, this.cancellationToken);

            List<FeedResponse<T>>[] queryResponses = await this.ReadManyTaskHelperAsync<FeedResponse<T>>(partitionKeyRangeItemMap,
                                                                this.GenerateTypedResponsesForPartitionAsync<T>,
                                                                trace);

            return this.CombineFeedResponseFromQueryResponses(queryResponses, trace);
        }

        internal Task<List<TResult>[]> ReadManyTaskHelperAsync<TResult>(IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap,
                                        Func<List<(string, PartitionKey)>, PartitionKeyRange, ITrace, Task<List<TResult>>> generateResponsesForPartition,
                                        ITrace trace)
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(0, this.maxConcurrency);
            List<Task<List<TResult>>> tasks = new List<Task<List<TResult>>>();

            foreach (KeyValuePair<PartitionKeyRange, List<(string, PartitionKey)>> entry in partitionKeyRangeItemMap)
            {
                using (ITrace childTrace = trace.StartChild("Execute query for a partitionkeyrange", TraceComponent.Query, TraceLevel.Info))
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        // Only allow 'maxConcurrency' number of queries at a time
                        await semaphore.WaitAsync();

                        try
                        {
                            return await generateResponsesForPartition(entry.Value, entry.Key, childTrace);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
            }

            // Restore semaphore to max Count and allow tasks to run
            semaphore.Release(this.maxConcurrency);
            return Task.WhenAll(tasks);
        }

        private async Task<IDictionary<PartitionKeyRange, List<(string, PartitionKey)>>> CreatePartitionKeyRangeItemListMapAsync(
            IReadOnlyList<(string, PartitionKey)> items,
            CancellationToken cancellationToken = default)
        {
            CollectionRoutingMap collectionRoutingMap = await this.container.GetRoutingMapAsync(cancellationToken);

            IDictionary<PartitionKeyRange, List<(string, PartitionKey)>> partitionKeyRangeItemMap = new
                Dictionary<PartitionKeyRange, List<(string, PartitionKey)>>();

            foreach ((string id, PartitionKey pk) item in items)
            {
                string effectivePartitionKeyValue = item.pk.InternalKey.GetEffectivePartitionKeyString(this.partitionKeyDefinition);
                PartitionKeyRange partitionKeyRange = collectionRoutingMap.GetRangeByEffectivePartitionKey(effectivePartitionKeyValue);
                if (partitionKeyRangeItemMap.TryGetValue(partitionKeyRange, out List<(string, PartitionKey)> itemList))
                {
                    itemList.Add(item);
                }
                else
                {
                    List<(string, PartitionKey)> newList = new List<(string, PartitionKey)> { item };
                    partitionKeyRangeItemMap[partitionKeyRange] = newList;
                }
            }

            return partitionKeyRangeItemMap;
        }

        private ResponseMessage CombineStreamsFromQueryResponses(List<ResponseMessage>[] queryResponses,
                                                                 string collectionRid,
                                                                 ITrace trace)
        {
            List<CosmosElement> cosmosElements = new List<CosmosElement>();
            double requestCharge = 0;
            foreach (List<ResponseMessage> responseMessagesForSinglePartition in queryResponses)
            {
                foreach (ResponseMessage responseMessage in responseMessagesForSinglePartition)
                {
                    using (responseMessage)
                    {
                        if (responseMessage is QueryResponse queryResponse)
                        {
                            cosmosElements.AddRange(queryResponse.CosmosElements);
                            requestCharge += queryResponse.Headers.RequestCharge;
                        }
                        else
                        {
                            throw new InvalidOperationException("Read Many is being used with Query");
                        }
                    }
                }
            }

            ResponseMessage combinedResponseMessage = new ResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = CosmosElementSerializer.ToStream(collectionRid, cosmosElements, ResourceType.Document),
                Trace = trace
            };
            combinedResponseMessage.Headers.RequestCharge = requestCharge;
            return combinedResponseMessage;
        }

        private FeedResponse<T> CombineFeedResponseFromQueryResponses<T>(List<FeedResponse<T>>[] queryResponses,
                                                                         ITrace trace)
        {
            // Implement new IEnumerable to avoid combining the array of lists
            ReadManyFeedResponseEnumerable<T> enumerable = new ReadManyFeedResponseEnumerable<T>(queryResponses);
            (int count, double requestCharge) = enumerable.GetCountAndRequestCharge();
            Headers headers = new Headers
            {
                RequestCharge = requestCharge
            };

            return new ReadFeedResponse<T>(System.Net.HttpStatusCode.OK,
                                        enumerable,
                                        count,
                                        headers,
                                        new CosmosTraceDiagnostics(trace));
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
                object[] pkValues = items[i].Item2.InternalKey.ToObjectArray();

                if (pkValues.Length != this.partitionKeyDefinition.Paths.Count)
                {
                    throw new ArgumentException("Number of components in the partition key value does not match the definition.");
                }

                string pkParamName = "@param_pk" + i;
                string idParamName = "@param_id" + i;
                sqlParameters.Add(new SqlParameter(idParamName, items[i].Item1));

                queryStringBuilder.Append("( ");
                queryStringBuilder.Append("c.id = ");
                queryStringBuilder.Append(idParamName);
                for (int j = 0; j < this.partitionKeySelectors.Count; j++)
                {
                    queryStringBuilder.Append(" AND ");
                    queryStringBuilder.Append("c");
                    queryStringBuilder.Append(this.partitionKeySelectors[j]);
                    queryStringBuilder.Append(" = ");

                    string pkParamNameForSinglePath = pkParamName + j;
                    sqlParameters.Add(new SqlParameter(pkParamNameForSinglePath, pkValues[j]));
                    queryStringBuilder.Append(pkParamNameForSinglePath);
                }

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

        private List<string> CreatePkSelectors(PartitionKeyDefinition partitionKeyDefinition)
        {
            List<string> pathSelectors = new List<string>();
            foreach (string path in partitionKeyDefinition.Paths)
            {
                IReadOnlyList<string> pathParts = PathParser.GetPathParts(path);
                List<string> modifiedPathParts = new List<string>();

                foreach (string pathPart in pathParts)
                {
                    modifiedPathParts.Add("[\"" + pathPart + "\"]");
                }

                string selector = String.Join(string.Empty, modifiedPathParts);
                pathSelectors.Add(selector);
            }

            return pathSelectors;
        }

        private async Task<List<ResponseMessage>> GenerateStreamResponsesForPartitionAsync(List<(string, PartitionKey)> items,
                                                                                  PartitionKeyRange partitionKeyRange,
                                                                                  ITrace trace)
        {
            QueryDefinition queryDefinition = ((this.partitionKeySelectors.Count == 1) && (this.partitionKeySelectors[0] == "[\"id\"]")) ?
                                                   this.CreateReadManyQueryDefifnitionForId(items) :
                                                   this.CreateReadManyQueryDefifnitionForOther(items);

            List<ResponseMessage> pages = new List<ResponseMessage>();
            FeedIteratorInternal feedIterator = (FeedIteratorInternal)this.container.GetItemQueryStreamIterator(
                                                    new FeedRangeEpk(partitionKeyRange.ToRange()),
                                                    queryDefinition,
                                                    continuationToken: null,
                                                    requestOptions: this.readManyRequestOptions?.ConvertToQueryRequestOptions());
            while (feedIterator.HasMoreResults)
            {
                ResponseMessage responseMessage = await feedIterator.ReadNextAsync(trace, this.cancellationToken);
                pages.Add(responseMessage);
            }

            return pages;
        }

        private async Task<List<FeedResponse<T>>> GenerateTypedResponsesForPartitionAsync<T>(List<(string, PartitionKey)> items,
                                                                          PartitionKeyRange partitionKeyRange,
                                                                          ITrace trace)
        {
            QueryDefinition queryDefinition = ((this.partitionKeySelectors.Count == 1) && (this.partitionKeySelectors[0] == "[\"id\"]")) ?
                                                   this.CreateReadManyQueryDefifnitionForId(items) :
                                                   this.CreateReadManyQueryDefifnitionForOther(items);

            List<FeedResponse<T>> pages = new List<FeedResponse<T>>();
            FeedIteratorInternal<T> feedIterator = (FeedIteratorInternal<T>)this.container.GetItemQueryIterator<T>(
                                                    new FeedRangeEpk(partitionKeyRange.ToRange()),
                                                    queryDefinition,
                                                    continuationToken: null,
                                                    requestOptions: this.readManyRequestOptions?.ConvertToQueryRequestOptions());
            while (feedIterator.HasMoreResults)
            {
                FeedResponse<T> feedResponse = await feedIterator.ReadNextAsync(trace, this.cancellationToken);
                pages.Add(feedResponse);
            }

            return pages;
        }

        private class ReadManyFeedResponseEnumerable<T> : IEnumerable<T>
        {
            private readonly List<FeedResponse<T>>[] queryResponses;

            public ReadManyFeedResponseEnumerable(List<FeedResponse<T>>[] queryResponses)
            {
                this.queryResponses = queryResponses;
            }

            public IEnumerator<T> GetEnumerator()
            {
                foreach (List<FeedResponse<T>> singlePartitionResponses in this.queryResponses)
                {
                    foreach (FeedResponse<T> feedResponse in singlePartitionResponses)
                    {
                        foreach (T item in feedResponse)
                        {
                            yield return item;
                        }
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public (int, double) GetCountAndRequestCharge()
            {
                int count = 0;
                double requestCharge = 0;

                foreach (List<FeedResponse<T>> singlePartitionResponses in this.queryResponses)
                {
                    foreach (FeedResponse<T> feedResponse in singlePartitionResponses)
                    {
                        requestCharge += feedResponse.RequestCharge;
                        count += feedResponse.Count;
                    }
                }

                return (count, requestCharge);
            }
        }
    }
}
