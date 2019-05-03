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
    }
}