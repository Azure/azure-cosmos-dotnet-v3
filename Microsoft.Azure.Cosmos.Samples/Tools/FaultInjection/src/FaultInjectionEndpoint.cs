//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;
    using Microsoft.Azure.Cosmos;

    /// <summary>
    /// Fault Injection Endpoint
    /// </summary>
    public sealed class FaultInjectionEndpoint
    {
        private readonly string databaseName;
        private readonly string containerName;
        private readonly FeedRange feedRange;
        private readonly bool includePrimary;
        private readonly int replicaCount;

        internal static FaultInjectionEndpoint Empty = new FaultInjectionEndpoint(
            string.Empty, 
            string.Empty, 
            new FeedRangePartitionKey(new PartitionKey()), false, 0);

        /// <summary>
        /// Creates a <see cref="FaultInjectionEndpoint"/>.
        /// </summary>
        /// <param name="databaseName">The database name.</param>
        /// <param name="containerName">The container name.</param>
        /// <param name="feedRange">The <see cref="FeedRange"/>.</param>
        /// <param name="includePrimary">Indicates wether primary replica can be used</param>
        /// <param name="replicaCount">Replica count.</param>
        public FaultInjectionEndpoint(
            string databaseName,
            string containerName,
            FeedRange feedRange,
            bool includePrimary,
            int replicaCount)
        { 
            this.databaseName = databaseName;
            this.containerName = containerName;
            this.feedRange = feedRange;
            this.includePrimary = includePrimary;
            this.replicaCount = replicaCount;
        }

        /// <summary>
        /// Get the FeedRange.
        /// </summary>
        /// <returns>the <see cref="FeedRange"/></returns>
        public FeedRange GetFeedRange()
        {
            return this.feedRange;
        }

        /// <summary>
        /// Get the flag indicating if primary replica address can be used.
        /// </summary>
        /// <returns>The flag indicating if a primary replica address can be used.</returns>
        public bool IsIncludePrimary()
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

        public string GetResoureName()
        {
            return $"dbs/{this.databaseName}/colls/{this.containerName}";
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>A string represeting the <see cref="FaultInjectionEndpoint"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "\"FaultInjectionEndpoint\":{{ \"ResourceName\": \"{0}\", \"FeedRange\": \"{1}\", \"IncludePrimary\": \"{2}\", \"ReplicaCount\": \"{3}\"}}",
                this.GetResoureName(),
                this.feedRange,
                this.includePrimary,
                this.replicaCount);
        }
    }
}
