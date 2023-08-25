//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents a partition key range in the Azure Cosmos DB service.
    /// </summary>
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class PartitionKeyRange : Resource, IEquatable<PartitionKeyRange>
    {
        // This is only used in Gateway and corresponds to the value set in the backend.
        // Client must not use this value, as it must use whatever comes in address resolution response.
        internal const string MasterPartitionKeyRangeId = "M";

        /// <summary>
        /// Represents the minimum possible value of a PartitionKeyRange in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MinInclusive)]
        internal string MinInclusive
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.MinInclusive);
            }
            set
            {
                base.SetValue(Constants.Properties.MinInclusive, value);
            }
        }

        /// <summary>
        /// Represents maximum exclusive value of a PartitionKeyRange (the upper, but not including this value, boundary of PartitionKeyRange)
        /// in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.MaxExclusive)]
        internal string MaxExclusive
        {
            get
            {
                return base.GetValue<string>(Constants.Properties.MaxExclusive);
            }
            set
            {
                base.SetValue(Constants.Properties.MaxExclusive, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.RidPrefix)]
        internal int? RidPrefix
        {
            get
            {
                return base.GetValue<int?>(Constants.Properties.RidPrefix);
            }
            set
            {
                base.SetValue(Constants.Properties.RidPrefix, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.ThroughputFraction)]
        internal double ThroughputFraction
        {
            get
            {
                return base.GetValue<double>(Constants.Properties.ThroughputFraction);
            }
            set
            {
                base.SetValue(Constants.Properties.ThroughputFraction, value);
            }
        }

        [JsonProperty(PropertyName = Constants.Properties.PartitionKeyRangeStatus)]
        internal PartitionKeyRangeStatus Status
        {
            get
            {
                return base.GetValue<PartitionKeyRangeStatus>(Constants.Properties.PartitionKeyRangeStatus);
            }
            set
            {
                base.SetValue(Constants.Properties.PartitionKeyRangeStatus, value);
            }
        }

        /// <summary>
        /// Contains ids or parent ranges in the Azure Cosmos DB service.
        /// For example if range with id '1' splits into '2' and '3',
        /// then Parents for ranges '2' and '3' will be ['1'].
        /// If range '3' splits into '4' and '5', then parents for ranges '4' and '5'
        /// will be ['1', '3'].
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Parents)]
        public Collection<string> Parents
        {
            get
            {
                return base.GetValue<Collection<string>>(Constants.Properties.Parents);
            }
            set
            {
                base.SetValue(Constants.Properties.Parents, value);
            }
        }

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
                && this.MinInclusive.Equals(other.MinInclusive)
                && this.MaxExclusive.Equals(other.MaxExclusive)
                && (this.ThroughputFraction == other.ThroughputFraction);
        }
    }
}