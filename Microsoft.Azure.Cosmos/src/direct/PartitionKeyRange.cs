//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Represents a partition key range in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    public
#else
    public
#endif
    sealed class PartitionKeyRange : PlainResource, IEquatable<PartitionKeyRange>
    {
        // This is only used in Gateway and corresponds to the value set in the backend.
        // Client must not use this value, as it must use whatever comes in address resolution response.
        internal const string MasterPartitionKeyRangeId = "M";

        /// <summary>
        /// Represents the minimum possible value of a PartitionKeyRange in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.MinInclusive)]
        public string MinInclusive { get; set; }

        /// <summary>
        /// Represents maximum exclusive value of a PartitionKeyRange (the upper, but not including this value, boundary of PartitionKeyRange)
        /// in the Azure Cosmos DB service.
        /// </summary>
        [JsonPropertyName(Constants.Properties.MaxExclusive)]
        public string MaxExclusive { get; set; }

        [JsonPropertyName(Constants.Properties.RidPrefix)]
        public int? RidPrefix { get; set; }

        [JsonPropertyName(Constants.Properties.ThroughputFraction)]
        public double ThroughputFraction { get; set; }

#if !DOCDBCLIENT
        [JsonPropertyName(Constants.Properties.TargetThroughput)]
        internal double? TargetThroughput { get; set; }
#endif

        [JsonPropertyName(Constants.Properties.PartitionKeyRangeStatus)]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter<PartitionKeyRangeStatus>))]
        public PartitionKeyRangeStatus Status { get; set; }

        [JsonPropertyName(Constants.Properties.Lsn)]
        public long LSN { get; set; }

        /// <summary>
        /// Contains ids or parent ranges in the Azure Cosmos DB service.
        /// For example if range with id '1' splits into '2' and '3',
        /// then Parents for ranges '2' and '3' will be ['1'].
        /// If range '3' splits into '4' and '5', then parents for ranges '4' and '5'
        /// will be ['1', '3'].
        /// </summary>
        [JsonPropertyName(Constants.Properties.Parents)]
        public Collection<string> Parents { get; set; }

        /// <summary>
        /// Contains ids of owned archival pkranges in the Azure Cosmos DB service.
        /// For example, consider a range '1' owns archival reference to ['0'], to begin.
        /// If '1' splits into '2' (left) and '3' (right)
        /// '2' owns archival reference to ['0']
        /// '3' owns archival reference to ['1']
        /// </summary>
        [JsonPropertyName(Constants.Properties.OwnedArchivalPKRangeIds)]
        internal Collection<string> OwnedArchivalPKRangeIds { get; set; }

        internal Range<string> ToRange()
        {
            return new Range<string>(this.MinInclusive, this.MaxExclusive, true, false);
        }

        /// <summary>
        /// Determines whether this instance in the Azure Cosmos DB service and a specified object have the same value.
        /// </summary>
        /// <param name="obj">The object to compare to this instance</param>
        public override bool Equals(object obj)
        {
            return this.Equals(obj as PartitionKeyRange);
        }

        /// <summary>
        /// Returns the hash code for this instance in the Azure Cosmos DB service.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer hash code.
        /// </returns>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 0;
                hash = (hash * 397) ^ this.Id.GetHashCode();
                if (!string.IsNullOrEmpty(this.ResourceId))
                {
                    hash = (hash * 397) ^ this.ResourceId.GetHashCode();
                }
                hash = (hash * 397) ^ this.MinInclusive.GetHashCode();
                hash = (hash * 397) ^ this.MaxExclusive.GetHashCode();
                return hash;
            }
        }

        /// <summary>
        /// Determines whether this instance in the Azure Cosmos DB service and a specified PartitionKeyRange object have the same value.
        /// </summary>
        /// <param name="other">The PartitionKeyRange object to compare to this instance</param>
        public bool Equals(PartitionKeyRange other)
        {
            if (other == null)
            {
                return false;
            }

            return this.Id == other.Id
                && string.Equals(this.ResourceId, other.ResourceId, StringComparison.Ordinal)
                && this.MinInclusive.Equals(other.MinInclusive)
                && this.MaxExclusive.Equals(other.MaxExclusive)
#if !DOCDBCLIENT
                && (this.TargetThroughput == other.TargetThroughput)
#endif
                && (this.ThroughputFraction == other.ThroughputFraction);
        }
    }
}