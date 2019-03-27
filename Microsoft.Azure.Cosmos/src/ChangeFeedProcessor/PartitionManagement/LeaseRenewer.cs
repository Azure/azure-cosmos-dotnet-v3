//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.PartitionManagement
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