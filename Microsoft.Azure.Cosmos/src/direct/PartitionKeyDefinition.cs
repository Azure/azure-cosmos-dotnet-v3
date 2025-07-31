//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary> 
    /// Specifies a partition key definition for a particular path in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT && !COSMOS_GW_AOT
    internal
#else
    public
#endif
    sealed class PartitionKeyDefinition
    {
        /// <summary>
        /// Gets or sets the paths to be partitioned in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// The path to be partitioned.
        /// </value>
        [JsonPropertyName(Constants.Properties.Paths)]
        public Collection<string> Paths { get; set; }

        /// <summary>
        /// Gets or sets the kind of partitioning to be applied in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PartitionKind"/> enumeration.
        /// </value>
        [JsonPropertyName(Constants.Properties.PartitionKind)]
        [JsonConverter(typeof(JsonStringEnumConverter<PartitionKind>))]
        internal PartitionKind Kind { get; set; }

        /// <summary>
        /// Gets or sets version of the partitioning scheme to be applied on the partition key
        /// </summary>
        /// <value>
        /// One of the values of the <see cref="T:Microsoft.Azure.Documents.PartitionKeyDefinitionVersion"/> enumeration. 
        /// </value>
        [JsonPropertyName(Constants.Properties.PartitionKeyDefinitionVersion)]
        [JsonConverter(typeof(JsonStringEnumConverter<PartitionKeyDefinitionVersion>))]
        public PartitionKeyDefinitionVersion? Version { get; set; }

        /// <summary>
        /// Gets whether the partition key definition in the collection is system inserted key
        /// in the Azure Cosmos DB service.
        /// </summary>
        /// <value>
        /// </value>
        [JsonPropertyName(Constants.Properties.SystemKey)]
        public bool? IsSystemKey { get; set; }

        internal void Validate()
        {
#if !COSMOS_GW_AOT
            base.Validate();
            base.GetValue<int?>(Constants.Properties.PartitionKeyDefinitionVersion);
            base.GetValue<Collection<string>>(Constants.Properties.Paths);
#endif
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

            if (!pkd1.Paths.SequenceEqual(pkd2.Paths))
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
