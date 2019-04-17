//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;

    internal class ChangeFeedResultSetIterator<T> : CosmosDefaultResultSetIterator<T>
    {
        internal ChangeFeedResultSetIterator(
            int? maxItemCount,
            string continuationToken,
            CosmosQueryRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null) : base(maxItemCount, continuationToken, options, nextDelegate, state)
        {
        }

        internal static CosmosQueryResponse<TInput> CreateCosmosQueryFeedResponse<TInput>(
            CosmosResponseMessage cosmosResponseMessage,
            CosmosJsonSerializer jsonSerializer)
        {
            using (cosmosResponseMessage)
            {
                // Throw the exception if the query failed: EnsureSuccessStatusCode only
                // validates 200-299. 304 is valid for Changefeed so do not throw on that.
                if (cosmosResponseMessage.StatusCode != HttpStatusCode.NotModified)
                {
                    cosmosResponseMessage.EnsureSuccessStatusCode();
                }

                string continuationToken = cosmosResponseMessage.Headers.ETag;
                bool hasMoreResults = ChangeFeedResultSetStreamIterator.GetHasMoreResults(continuationToken, cosmosResponseMessage.StatusCode);

                return CosmosQueryResponse<TInput>.CreateResponse<TInput>(
                    responseMessageHeaders: cosmosResponseMessage.Headers,
                    stream: cosmosResponseMessage.Content,
                    jsonSerializer: jsonSerializer,
                    continuationToken: continuationToken,
                    hasMoreResults: hasMoreResults);
            }
        }
    }
}