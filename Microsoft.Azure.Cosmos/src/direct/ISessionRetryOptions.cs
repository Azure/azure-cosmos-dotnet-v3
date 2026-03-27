namespace Microsoft.Azure.Documents
{
    using System;

    internal interface ISessionRetryOptions
    {

        /// <summary>
        /// Sets the minimum retry time for 404/1002 retries within each region for read and write operations. 
        /// The minimum value is 100ms - this minimum is enforced to provide a way for the local region to catch-up on replication lag. The default value is 500ms - as a recommendation ensure that this value is higher than the steady-state
        /// replication latency between the regions you chose
        /// </summary>
        TimeSpan MinInRegionRetryTime { get; }

        /// <summary>
        /// Sets the maximum number of retries within each region for read and write operations. The minimum value is 1 - the backoff time for the last in-region retry will ensure that the total retry time within the
        /// region is at least the min. in-region retry time.
        /// </summary>
        int MaxInRegionRetryCount { get; }


        /// <summary>
        /// hints which guide SDK-internal retry policies on how early to switch retries to a different region.
        /// </summary>
        Boolean RemoteRegionPreferred { get; }
    }
}