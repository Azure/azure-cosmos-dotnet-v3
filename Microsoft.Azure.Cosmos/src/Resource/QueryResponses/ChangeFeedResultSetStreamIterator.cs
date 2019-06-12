//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class ChangeFeedResultSetStreamIterator : FeedIteratorCore
    {
        internal ChangeFeedResultSetStreamIterator(
            int? maxItemCount,
            string continuationToken,
            QueryRequestOptions options,
            NextResultSetDelegate nextDelegate,
            object state = null)
            : base(
                maxItemCount,
                continuationToken, 
                options, 
                nextDelegate,
                state)
        {
        }
    }
}