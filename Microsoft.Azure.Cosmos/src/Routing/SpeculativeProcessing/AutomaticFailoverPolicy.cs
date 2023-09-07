namespace Microsoft.Azure.Cosmos.Routing.SpeculativeProcessing
{
    using System;

    /// <summary>
    /// Automatic Failover Policy
    /// </summary>
    internal class AutomaticFailoverPolicy
    { 
        private readonly TimeSpan threshold;

        private bool enabled = true;

        /// <summary>
        /// Creates Automatic Failover Policy
        /// </summary>
        /// <param name="fallbackAfterTimeSpan"></param>
        internal AutomaticFailoverPolicy(TimeSpan fallbackAfterTimeSpan)
        {
            this.threshold = fallbackAfterTimeSpan;
        }

        /// <summary>
        /// Enables Automatic Failover
        /// </summary>
        internal void Enable()
        {
            this.enabled = true;
        }

        /// <summary>
        /// Disables Automatic Failover
        /// </summary>
        internal void Disable()
        {
            this.enabled = false;
        }

        /// <summary>
        /// Check to see if Automatic Failover is enabled
        /// </summary>
        /// <returns>a bool representing if automatic failover is enabled</returns>
        internal bool IsEnabled()
        {
            return this.enabled;
        }

        /// <summary>
        /// The threshold of when to failover.
        /// </summary>
        /// <returns>the threshold.</returns>
        internal TimeSpan GetThreshold()
        {
            return this.threshold;
        }

        /// <summary>
        /// Marks the endpoint as unavailable for read and write
        /// </summary>
        /// <param name="requestDestination"></param>
        /// <param name="globalEndpointManager"></param>
        /// <returns>whether the regions was failed over sucessfully</returns>
        internal bool Failover(Uri requestDestination, GlobalEndpointManager globalEndpointManager)
        {
            if (this.enabled)
            {
                globalEndpointManager.MarkEndpointUnavailableForRead(requestDestination);
                globalEndpointManager.MarkEndpointUnavailableForWrite(requestDestination);
                return true;
            }

            return false;          
        }

    }
}
