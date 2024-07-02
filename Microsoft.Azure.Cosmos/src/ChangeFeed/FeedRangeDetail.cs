//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    /// <summary>
    /// The feed range details.
    /// </summary>
    public class FeedRangeDetail
    {
        /// <summary>
        /// Gets the min inclusive.
        /// </summary>
        public string MinInclusive { get; private set; }

        /// <summary>
        /// Gets the max exclusive.
        /// </summary>
        public string MaxExclusive { get; private set; }

        /// <summary>
        /// Gets the collection resource id.
        /// </summary>
        public string CollectionRid { get; private set; }

        /// <summary>
        /// Creates a new feed range detail.
        /// </summary>
        /// <param name="minInclusive"></param>
        /// <param name="maxExclusive"></param>
        /// <param name="collectionRid"></param>
        /// <returns>A immutable feed range detail.</returns>
        public static FeedRangeDetail Create(string minInclusive, string maxExclusive, string collectionRid)
        {
            return new FeedRangeDetail(
                minInclusive: minInclusive,
                maxExclusive: maxExclusive,
                collectionRid: collectionRid);
        }
        /// <summary>
        /// The construtor for the feed range detail.
        /// </summary>
        /// <param name="minInclusive">The minInclusive for the feed range.</param>
        /// <param name="maxExclusive">The maxExclusive for the feed range.</param>
        /// <param name="collectionRid">The collection resource id for the feed range.</param>
        private FeedRangeDetail(string minInclusive, string maxExclusive, string collectionRid)
        {
            this.MinInclusive = minInclusive;
            this.MaxExclusive = maxExclusive;
            this.CollectionRid = collectionRid;
        }
    }
}
