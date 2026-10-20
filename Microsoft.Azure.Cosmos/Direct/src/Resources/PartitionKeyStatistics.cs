//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;

    /// <summary>
    /// Represents statistics of a partition key in the Azure Cosmos DB service.
    /// </summary>
    /// <remarks>
    /// This is reported based on a sub-sampling of partition keys within the collection and hence these are approximate. If your partition keys are below 1GB of storage, they may not show up in the reported statistics.
    /// For usage, please refer to the example in <see cref="Microsoft.Azure.Documents.DocumentCollection.PartitionKeyRangeStatistics"/>.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
#if COSMOSCLIENT
    internal
#else
    public
#endif
    sealed class PartitionKeyStatistics
    {
        /// <summary>
        /// Gets the partition key in the Azure Cosmos DB service.
        /// </summary>
        public PartitionKey PartitionKey
        {
            get
            {
                return PartitionKey.FromInternalKey(this.PartitionKeyInternal);
            }
        }

        /// <summary>
        /// Gets the size of the partition key in the Azure Cosmos DB service.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.SizeInKB)]
        public long SizeInKB
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the stringified version of <see cref="PartitionKeyStatistics"/> object in the Azure Cosmos DB service.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }

        [JsonProperty(PropertyName = Constants.Properties.PartitionKey)]
        internal PartitionKeyInternal PartitionKeyInternal
        {
            get;
            private set;
        }
    }
}
