//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Net;

    internal class ChangeFeedResultSetIterator<T> : FeedIteratorCore<T>
    {
        internal ChangeFeedResultSetIterator(
            int? maxItemCount,
            string continuationToken,
            QueryRequestOptions options,
            FeedIteratorCore.NextResultSetDelegate nextDelegate,
            Func<CosmosResponseMessage, FeedResponse<T>> responseCreator,
            object state = null)
            : base(
                  maxItemCount, 
                  continuationToken, 
                  options, 
                  nextDelegate, 
                  responseCreator, 
                  state)
        {
        }
    }
}