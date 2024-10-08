//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// This class is used to build a <see cref="FaultInjectionEndpoint"/>
    /// </summary>
    public sealed class FaultInjectionEndpointBuilder
    {
        private readonly string databaseName;
        private readonly string containerName;
        private readonly FeedRange feedRange;
        private int replicaCount = int.MaxValue;
        private bool includePrimary = true;

        /// <summary>
        /// Used to create a <see cref="FaultInjectionEndpoint"/>
        /// </summary>
        /// <param name="databaseName">the database name.</param>
        /// <param name="containerName">the container name.</param>
        /// <param name="feedRange">the <see cref="FeedRange"/>.</param>
        public FaultInjectionEndpointBuilder(string databaseName, string containerName, FeedRange feedRange)
        {
            this.databaseName = databaseName;
            this.containerName = containerName;
            this.feedRange = feedRange;
        }

        /// <summary>
        /// Set the replica count of the <see cref="FaultInjectionEndpoint"/>.
        /// </summary>
        /// <param name="replicaCount">int representing the replica count.</param>
        /// <returns>the <see cref="FaultInjectionEndpointBuilder"/>.</returns>
        public FaultInjectionEndpointBuilder WithReplicaCount(int replicaCount)
        {
            if (replicaCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(replicaCount), "Argument 'replicaCount' cannot be negative.");
            }

            this.replicaCount = replicaCount;
            return this;
        }

        /// <summary>
        /// Flag to indicate whether primary replica address can be used. 
        /// </summary>
        /// <param name="includePrimary"> flag to indicate whether primary addresses can be used.</param>
        /// <returns>the <see cref="FaultInjectionEndpointBuilder"/>.</returns>
        public FaultInjectionEndpointBuilder WithIncludePrimary(bool includePrimary)
        {
            this.includePrimary = includePrimary;
            return this;
        }

        /// <summary>
        /// Creates a new <see cref="FaultInjectionEndpoint"/>.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionEndpoint"/>.</returns>
        public FaultInjectionEndpoint Build()
        {
            return new FaultInjectionEndpoint(this.databaseName, this.containerName, this.feedRange, this.includePrimary, this.replicaCount);
        }
    }
}
