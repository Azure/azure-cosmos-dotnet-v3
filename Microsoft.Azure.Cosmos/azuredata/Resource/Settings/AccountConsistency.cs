//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Azure.Cosmos
{
    /// <summary>
    /// Represents the consistency policy of a database account of the Azure Cosmos DB service.
    /// </summary>
    public sealed class AccountConsistency
    {
        internal AccountConsistency()
        {
        }

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
        public ConsistencyLevel DefaultConsistencyLevel { get; internal set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms difference in sequence numbers (aka version) in the Azure Cosmos DB service.
        /// </summary>
        public int MaxStalenessPrefix { get; internal set; }

        /// <summary>
        /// For bounded staleness consistency, the maximum allowed staleness
        /// in terms time interval in the Azure Cosmos DB service.
        /// </summary>
        public int MaxStalenessIntervalInSeconds { get; internal set; }

        internal ConsistencyLevel ToDirectConsistencyLevel()
        {
            return (ConsistencyLevel)this.DefaultConsistencyLevel;
        }
    }
}
