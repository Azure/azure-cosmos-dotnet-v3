//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents the consistency policy of a database account of the Azure Cosmos DB service.
    /// </summary>
    public class AccountConsistency
    {
        private const ConsistencyLevel defaultDefaultConsistencyLevel = ConsistencyLevel.Session;

        internal const int DefaultMaxStalenessInterval = 5;
        internal const int DefaultMaxStalenessPrefix = 100;

        internal const int MaxStalenessIntervalInSecondsMinValue = 5;
        internal const int MaxStalenessIntervalInSecondsMaxValue = 86400;

        internal const int MaxStalenessPrefixMinValue = 10;
        internal const int MaxStalenessPrefixMaxValue = 1000000;

        /// <summary>
        /// Get or set the default consistency level in the Azure Cosmos DB service.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = Constants.Properties.DefaultConsistencyLevel)]
        public ConsistencyLevel DefaultConsistencyLevel { get; internal set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms difference in sequence numbers (aka version) in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MaxStalenessPrefix)]
        public int MaxStalenessPrefix { get; internal set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms time interval in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MaxStalenessIntervalInSeconds)]
        public int MaxStalenessIntervalInSeconds { get; internal set; }

        internal Documents.ConsistencyLevel ToDirectConsistencyLevel()
        {
            return (Documents.ConsistencyLevel)this.DefaultConsistencyLevel;
        }
    }
}
