//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.FaultInjection
{
    using System;

    /// <summary>
    /// Fault Injection Condition
    /// </summary>
    public sealed class FaultInjectionCondition
    {
        private readonly FaultInjectionOperationType operationType;
        private readonly FaultInjectionConnectionType connectionType;
        private readonly string region;
        private readonly FaultInjectionEndpoint endpoint;

        /// <summary>
        /// Creates a <see cref="FaultInjectionCondition"/>.
        /// </summary>
        /// <param name="operationType">Specifies which operation type rule will target.</param>
        /// <param name="connectionType">Specifies which connection type rule will target.</param>
        /// <param name="region">Specifies wich region the rule will target.</param>
        /// <param name="endpoint">Specifies which endpoint the rule will tareget.</param>
        public FaultInjectionCondition(
            FaultInjectionOperationType? operationType = null,
            FaultInjectionConnectionType? connectionType = null,
            string? region = null,
            FaultInjectionEndpoint? endpoint = null)
        {

            RegionNameMapper mapper = new RegionNameMapper();
            this.region = string.IsNullOrEmpty(region) ? string.Empty : mapper.GetCosmosDBRegionName(region);

            this.operationType = operationType ?? FaultInjectionOperationType.All;
            this.connectionType = this.IsMetadataOperationType()
                ? FaultInjectionConnectionType.Gateway 
                : connectionType ?? FaultInjectionConnectionType.All;
            this.endpoint = endpoint ?? FaultInjectionEndpoint.Empty;
        }

        /// <summary>
        /// The operation type the rule will target.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionOperationType"/>.</returns>
        public FaultInjectionOperationType GetOperationType() 
        { 
            return this.operationType;
        }

        /// <summary>
        /// The connection type the rule will target.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionConnectionType"/>.</returns>
        public FaultInjectionConnectionType GetConnectionType()
        {
            return this.connectionType;
        }

        /// <summary>
        /// The region the rule will target.
        /// Will return the empty string if the rule is not targeting a specific region.
        /// </summary>
        /// <returns>the region represented as a string.</returns>
        public string GetRegion()
        {
            return this.region;
        }

        /// <summary>
        /// The endpoint the rule will target.
        /// </summary>
        /// <returns>the <see cref="FaultInjectionEndpoint"/>.</returns>
        public FaultInjectionEndpoint GetEndpoint()
        {
            return this.endpoint;
        }

        /// <summary>
        /// To String method
        /// </summary>
        /// <returns>A string represeting the <see cref="FaultInjectionCondition"/>.</returns>
        public override string ToString()
        {
            return String.Format(
                "FaultInjectionCondition{{ OperationType: {0}, ConnectionType: {1}, Region: {2}, Endpoint: {3}",
                this.operationType,
                this.connectionType,
                this.region,  
                this.endpoint.ToString());
        }

        internal bool IsMetadataOperationType()
        {
            return this.operationType == FaultInjectionOperationType.MetadataContainer
                || this.operationType == FaultInjectionOperationType.MetadataDatabaseAccount
                || this.operationType == FaultInjectionOperationType.MetadataPartitionKeyRange
                || this.operationType == FaultInjectionOperationType.MetadataRefreshAddresses
                || this.operationType == FaultInjectionOperationType.MetadataQueryPlan;
        }
    }
}
