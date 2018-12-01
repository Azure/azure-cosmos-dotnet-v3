//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
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
                // Throw the exception if the query failed.
                cosmosResponseMessage.EnsureSuccessStatusCode();

                string continuationToken = ChangeFeedResultSetStreamIterator.GetContinuationToken(cosmosResponseMessage);
                bool hasMoreResults = ChangeFeedResultSetStreamIterator.GetHasMoreResults(continuationToken, cosmosResponseMessage.Headers.ContentLengthAsLong);

                return CosmosQueryResponse<TInput>.CreateResponse<TInput>(
                    stream: cosmosResponseMessage.Content,
                    jsonSerializer: jsonSerializer,
                    continuationToken: continuationToken,
                    hasMoreResults: hasMoreResults);
            }
        }
    }
}