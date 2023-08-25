// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ReadFeed.Pagination
{
    internal sealed class ReadFeedBeginningState : ReadFeedState
    {
        public static readonly ReadFeedBeginningState Singleton = new ReadFeedBeginningState(); 
        
        private ReadFeedBeginningState()
        {
        }
    }
}
