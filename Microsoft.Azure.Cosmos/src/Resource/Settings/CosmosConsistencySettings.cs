//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Globalization;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the consistency policy of a database account of the Azure Cosmos DB service.
    /// </summary>
    public sealed class CosmosConsistencySettings
    {
        private const ConsistencyLevel defaultDefaultConsistencyLevel = ConsistencyLevel.Session;

        internal const int DefaultMaxStalenessInterval = 5;
        internal const int DefaultMaxStalenessPrefix = 100;

        internal const int MaxStalenessIntervalInSecondsMinValue = 5;
        internal const int MaxStalenessIntervalInSecondsMaxValue = 86400;

        internal const int MaxStalenessPrefixMinValue = 10;
        internal const int MaxStalenessPrefixMaxValue = 1000000;

        /// <summary>
        /// Default constructor for ConsistencyPolicy class in the Azure Cosmos DB service.
        /// </summary>
        public CosmosConsistencySettings()
        {
        }

        /// <summary>
        /// Get or set the default consistency level in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.DefaultConsistencyLevel)]
        public ConsistencyLevel DefaultConsistencyLevel { get; set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms difference in sequence numbers (aka version) in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MaxStalenessPrefix)]
        public int MaxStalenessPrefix { get; set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms time interval in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MaxStalenessIntervalInSeconds)]
        public int MaxStalenessIntervalInSeconds { get; set; }

        internal Documents.ConsistencyLevel ToDirectConsistencyLevel()
        {
            return (Documents.ConsistencyLevel)this.DefaultConsistencyLevel;
        }
    }
}
