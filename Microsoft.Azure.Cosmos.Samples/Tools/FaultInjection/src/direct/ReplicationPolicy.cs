//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    /// <summary>
    /// Replication policy.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class ReplicationPolicy : JsonSerializable
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
        public int MaxReplicaSetSize
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.MaxReplicaSetSize, DefaultMaxReplicaSetSize);
            }
            set
            {
                base.SetValue(Constants.Properties.MaxReplicaSetSize, value);
            }
        }

        /// <summary>
        /// Minimum number of replicas to ensure availability
        /// of the partition.
        /// </summary>
        public int MinReplicaSetSize
        {
            get
            {
                return base.GetValue<int>(Constants.Properties.MinReplicaSetSize, DefaultMinReplicaSetSize);
            }
            set
            {
                base.SetValue(Constants.Properties.MinReplicaSetSize, value);
            }
        }

        /// <summary>
        /// Whether or not async replication is enabled.
        /// </summary>
        public bool AsyncReplication
        {
            get
            {
                return base.GetValue<bool>(Constants.Properties.AsyncReplication, DefaultAsyncReplication);
            }
            set
            {
                base.SetValue(Constants.Properties.AsyncReplication, value);
            }
        }

        internal override void Validate()
        {
            base.Validate();
            Helpers.ValidateNonNegativeInteger(Constants.Properties.MinReplicaSetSize, this.MinReplicaSetSize);
            Helpers.ValidateNonNegativeInteger(Constants.Properties.MinReplicaSetSize, this.MaxReplicaSetSize);
        }
    }
}
