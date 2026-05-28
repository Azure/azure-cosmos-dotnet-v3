// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Implementation of ISessionRetryOptions interface, do not want clients to subclass.
    /// </summary>
    internal sealed class SessionRetryOptions : ISessionRetryOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SessionRetryOptions"/> class.
        /// </summary>
        public SessionRetryOptions()
        {
            this.MinInRegionRetryTime = ConfigurationManager.GetMinRetryTimeInLocalRegionWhenRemoteRegionPreferred();
            this.MaxInRegionRetryCount = ConfigurationManager.GetMaxRetriesInLocalRegionWhenRemoteRegionPreferred();
        }
        /// <summary>
        /// Sets the minimum retry time for 404/1002 retries within each region for read and write operations. 
        /// The minimum value is 100ms - this minimum is enforced to provide a way for the local region to catch-up on replication lag. The default value is 500ms - as a recommendation ensure that this value is higher than the steady-state
        /// replication latency between the regions you chose
        /// </summary>
        public TimeSpan MinInRegionRetryTime { get; set; }

        /// <summary>
        /// Sets the maximum number of retries within each region for read and write operations. The minimum value is 1 - the backoff time for the last in-region retry will ensure that the total retry time within the
        /// region is at least the min. in-region retry time.
        /// </summary>
        public int MaxInRegionRetryCount { get; set; }

        /// <summary>
        /// hints which guide SDK-internal retry policies on how early to switch retries to a different region. If true, will retry all replicas once and add a minimum delay before switching to the next region.If false, it will
        /// retry in the local region up to 5s
        /// </summary>
        public bool RemoteRegionPreferred { get; set; } = false;

    }
}