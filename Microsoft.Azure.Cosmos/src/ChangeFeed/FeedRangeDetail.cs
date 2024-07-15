//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed
{
    /// <summary>
    /// The feed range details.
    /// </summary>
    internal class FeedRangeDetail
    {
        /// <summary>
        /// Gets the min inclusive.
        /// </summary>
        public FeedRange FeedRange { get; private set; }

        /// <summary>
        /// Gets the collection resource id.
        /// </summary>
        public string CollectionRid { get; private set; }

        /// <summary>
        /// Creates a new feed range detail.
        /// </summary>
        /// <param name="feedRange"></param>
        /// <param name="collectionRid"></param>
        /// <returns>A immutable feed range detail.</returns>
        public static FeedRangeDetail Create(FeedRange feedRange, string collectionRid)
        {
            return new FeedRangeDetail(
                feedRange: feedRange,
                collectionRid: collectionRid);
        }

        /// <summary>
        /// The construtor for the feed range detail.
        /// </summary>
        /// <param name="feedRange">The minInclusive for the feed range.</param>
        /// <param name="collectionRid">The collection resource id for the feed range.</param>
        private FeedRangeDetail(FeedRange feedRange, string collectionRid)
        {
            this.FeedRange = feedRange;
            this.CollectionRid = collectionRid;
        }
    }
}
