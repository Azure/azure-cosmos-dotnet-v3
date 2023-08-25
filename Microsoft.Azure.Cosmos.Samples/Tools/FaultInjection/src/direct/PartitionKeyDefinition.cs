//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary> 
    /// Specifies a partition key definition for a particular path in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class PartitionKeyDefinition : JsonSerializable
    {
        private Collection<string> paths;

        private PartitionKind? kind;

        /// <summary>
        /// Gets or sets the paths to be partitioned in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be partitioned.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.Paths)]
        public Collection<string> Paths
        {
            get
            {
                // Thread safe initialization. Collection is cached and PartitionKey can be looked up from multiple threads.
                if (this.paths == null)
                {
                    this.paths = base.GetValue<Collection<string>>(Constants.Properties.Paths) ?? new Collection<string>();
                }

                return this.paths;
            }
            set
            {
                this.paths = value;
                base.SetValue(Constants.Properties.Paths, value);
            }
        }

        /// <summary>
        /// Gets or sets the kind of partitioning to be applied in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PartitionKind"/> enumeration.
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKind)]
        [JsonConverter(typeof(StringEnumConverter))]
        internal PartitionKind Kind
        {
            get
            {
                if (!this.kind.HasValue)
                {
                    this.kind = base.GetValue<PartitionKind>(Constants.Properties.PartitionKind, PartitionKind.Hash);
                }

                return this.kind.Value;
            }
            set
            {
                this.kind = null;
                base.SetValue(Constants.Properties.PartitionKind, value.ToString());
            }
        }

        /// <summary>
        /// Gets or sets version of the partitioning scheme to be applied on the partition key
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PartitionKeyDefinitionVersion"/> enumeration. 
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.PartitionKeyDefinitionVersion, DefaultValueHandling = DefaultValueHandling.Ignore )]
        public PartitionKeyDefinitionVersion? Version
        {
            get
            {
                return (PartitionKeyDefinitionVersion?)base.GetValue<int?>(Constants.Properties.PartitionKeyDefinitionVersion);
            }
            set
            {
                base.SetValue(Constants.Properties.PartitionKeyDefinitionVersion, (int?)value);
            }
        }

        /// <summary>
        /// Gets whether the partition key definition in the collection is system inserted key
        /// in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// </value>
        [JsonProperty(PropertyName = Constants.Properties.SystemKey, DefaultValueHandling = DefaultValueHandling.Ignore)]
        internal bool? IsSystemKey
        {
            get
            {
                return base.GetValue<bool?>(Constants.Properties.SystemKey);
            }
            set
            {
                base.SetValue(Constants.Properties.SystemKey, value);
            }
        }

        internal override void OnSave()
        {
            if (this.paths != null)
            {
                base.SetValue(Constants.Properties.Paths, this.paths);
            }

            if (this.kind != null)
            {
                base.SetValue(Constants.Properties.PartitionKind, this.kind.ToString());
            }
        }

        internal override void Validate()
        {
            base.Validate();
            base.GetValue<int?>(Constants.Properties.PartitionKeyDefinitionVersion);
            base.GetValue<Collection<string>>(Constants.Properties.Paths);
        }

        internal static bool AreEquivalent(
            PartitionKeyDefinition pkd1,
            PartitionKeyDefinition pkd2)
        {
            if (pkd1.Kind != pkd2.Kind)
            {
                return false;
            }

            if (pkd1.Version != pkd2.Version)
            {
                return false;
            }

            if (!pkd1.Paths.OrderBy(i => i).SequenceEqual(pkd2.Paths.OrderBy(i => i)))
            {
                return false;
            }

            if (pkd1.IsSystemKey != pkd2.IsSystemKey)
            {
                return false;
            }

            return true;
        }
    }
}
