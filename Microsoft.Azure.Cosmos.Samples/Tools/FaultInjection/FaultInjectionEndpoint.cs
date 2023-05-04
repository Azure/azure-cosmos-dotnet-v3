//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Fault Injection Endpoint
    /// </summary>
    public sealed class FaultInjectionEndpoint
    {
        private readonly FeedRange feedRange;
        private readonly bool includePrimary;
        private readonly int replicaCount;

        /// <summary>
        /// Creates a <see cref="FaultInjectionEndpoint"/>.
        /// </summary>
        /// <param name="feedRange">The <see cref="FeedRange"/>.</param>
        /// <param name="includePrimary">Indicates wether primary replica can be used</param>
        /// <param name="replicaCount">Replica count.</param>
        public FaultInjectionEndpoint(
            FeedRange feedRange,
            bool includePrimary,
            int replicaCount)
        {
            this.feedRange = feedRange;
            this.includePrimary = includePrimary;
            this.replicaCount = replicaCount;
        }

        /// <summary>
        /// Get the FeedRange
        /// </summary>
        /// <returns>the <see cref="FeedRange"/></returns>
        public FeedRange GetFeedRange()
        {
            return this.feedRange;
        }

        /// <summary>
        /// Get the flag indicating if primary replica address can be used.
        /// </summary>
        /// <returns>the flag indicating if a primary replica address can be used.</returns>
        public bool isIncludePrimary()
        {
            return this.includePrimary;
        }

        /// <summary>
        /// Gets the replica count. Used to inidcate how many physical addresses can be applied to the fault injection rule.
        /// </summary>
        /// <returns>an int, the replica count</returns>
        public int GetReplicaCount()
        { 
            return this.replicaCount; 
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>A string represeting the <see cref="FaultInjectionEndpoint"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionEndpoint{{ FeedRange: {0}, IncludePrimary: {1}, ReplicaCount{2}",
                this.feedRange,
                this.includePrimary,
                this.replicaCount);
        }
    }
}
