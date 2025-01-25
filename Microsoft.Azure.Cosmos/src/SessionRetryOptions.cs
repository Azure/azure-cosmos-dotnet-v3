namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// Implementation of ISessionRetryOptions interface, do not want clients to subclass.
    /// </summary>
    public sealed class SessionRetryOptions : ISessionRetryOptions
    {
        /// <summary>
        /// Sets the minimum retry time for 404/1002 retries within each region for read and write operations. 
        /// The minimum value is 100ms - this minimum is enforced to provide a way for the local region to catch-up on replication lag. The default value is 500ms - as a recommendation ensure that this value is higher than the steady-state
        /// replication latency between the regions you chose
        /// </summary>
        public TimeSpan MinInRegionRetryTime { get; set; } = ConfigurationManager.GetMinRetryTimeInLocalRegionWhenRemoteRegionPreferred();

        /// <summary>
        /// Sets the maximum number of retries within each region for read and write operations. The minimum value is 1 - the backoff time for the last in-region retry will ensure that the total retry time within the
        /// region is at least the min. in-region retry time.
        /// </summary>
        public int MaxInRegionRetryCount { get; set; } = ConfigurationManager.GetMaxRetriesInLocalRegionWhenRemoteRegionPreferred();


        /// <summary>
        /// hints which guide SDK-internal retry policies on how early to switch retries to a different region.
        /// </summary>
        public Boolean RemoteRegionPreferred { get; set; } = false;


            
    }
}
