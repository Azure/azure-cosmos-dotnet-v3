//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement
{
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class LeaseRenewer
    {
        /// <summary>
        /// Starts the lease renewer
        /// </summary>
        public abstract Task RunAsync(CancellationToken cancellationToken);
    }
}