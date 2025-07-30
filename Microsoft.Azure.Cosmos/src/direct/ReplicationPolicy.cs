//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Replication policy.
    /// </summary>
#if COSMOSCLIENT && !COSMOS_GW_AOT
    internal
#else
    public
#endif
    sealed class ReplicationPolicy
    {
        private const int DefaultMaxReplicaSetSize = 4;
        private const int DefaultMinReplicaSetSize = 3; 
        private const bool DefaultAsyncReplication = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ReplicationPolicy()
        {
        }

        /// <summary>
        /// Maximum number of replicas for the partition.
        /// </summary>
        [JsonPropertyName(Constants.Properties.MaxReplicaSetSize)]
        public int MaxReplicaSetSize { get; set; } = DefaultMaxReplicaSetSize;

        /// <summary>
        /// Minimum number of replicas to ensure availability
        /// of the partition.
        /// </summary>
        [JsonPropertyName(Constants.Properties.MinReplicaSetSize)]
        public int MinReplicaSetSize { get; set; } = DefaultMinReplicaSetSize;


        /// <summary>
        /// Whether or not async replication is enabled.
        /// </summary>
        [JsonPropertyName(Constants.Properties.AsyncReplication)]
        public bool AsyncReplication { get; set; } = DefaultAsyncReplication;

        internal void Validate()
        {
            Helpers.ValidateNonNegativeInteger(Constants.Properties.MinReplicaSetSize, this.MinReplicaSetSize);
            Helpers.ValidateNonNegativeInteger(Constants.Properties.MinReplicaSetSize, this.MaxReplicaSetSize);
        }
    }
}
