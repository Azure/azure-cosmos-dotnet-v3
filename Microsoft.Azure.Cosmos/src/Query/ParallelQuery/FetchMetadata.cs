//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.ParallelQuery
{
    /// <summary>
    /// Contains metadata related to resolving a fetch operation on DocumentProducer.
    /// Instances are used to communicate metadata between DocumentProducer and it's consumer via
    /// the preCompleteFetchCallback and postCompleteFetchCallback callbacks.
    /// 
    /// DEVNOTE: Due to the way DocumentProducer works, this metadata is only shared with the consumer via the callbacks.
    /// This makes the aggregation of the metadata out-of-band from the consumption of the items which isn't the best approach.
    /// </summary>
    internal sealed class FetchMetadata
    {
        /// <summary>
        /// Gets the total number of items fetched.
        /// </summary>
        public int TotalItemsFetched { get; set; }

        /// <summary>
        /// Gets the RU usage to resolve the fetch op.
        /// </summary>
        public double ResourceUnitUsage { get; set; }

        /// <summary>
        /// Gets the QueryMetrcis for the fetch op.
        /// </summary>
        public QueryMetrics QueryMetrics { get; set; }

        /// <summary>
        /// Gets the total response lenght for the fetch op.
        /// </summary>
        public long TotalResponseLengthBytes { get; set; }
    }
}