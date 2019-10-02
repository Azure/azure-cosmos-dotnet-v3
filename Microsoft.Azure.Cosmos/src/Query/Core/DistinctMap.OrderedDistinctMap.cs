//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using Microsoft.Azure.Cosmos.CosmosElements;

    /// <summary>
    /// Partial wrapper
    /// </summary>
    internal abstract partial class DistinctMap
    {
        /// <summary>
        /// For distinct queries of the form:
        /// SELECT DISTINCT VALUE c.(blah) from c order by c.(blah)
        /// We can make an optimization, since the problem boils down to
        /// "How can you find all the distinct items in a sorted stream"
        /// Ex. "1, 1, 2, 2, 2, 3, 4, 4" -> "1, 2, 3, 4"
        /// The solution is that you only need to remember the previous item of the stream:
        /// foreach item in stream:
        ///     if item != previous item:
        ///         yield item
        /// This class accomplishes that by storing the previous hash and assuming the items come in sorted order.
        /// </summary>
        private sealed class OrderedDistinctMap : DistinctMap
        {
            /// <summary>
            /// The hash of the last item that was added to this distinct map.
            /// </summary>
            private UInt192 lastHash;

            /// <summary>
            /// Initializes a new instance of the OrderedDistinctMap class.
            /// </summary>
            /// <param name="lastHash">The previous hash from the previous continuation.</param>
            public OrderedDistinctMap(UInt192 lastHash)
            {
                this.lastHash = lastHash;
            }

            /// <summary>
            /// Adds a JToken to this map if it hasn't already been added.
            /// </summary>
            /// <param name="cosmosElement">The element to add.</param>
            /// <param name="hash">The hash of the token.</param>
            /// <returns>Whether or not the item was added to this Distinct Map.</returns>
            /// <remarks>This function assumes data is added in sorted order.</remarks>
            public override bool Add(CosmosElement cosmosElement, out UInt192? hash)
            {
                hash = DistinctMap.GetHash(cosmosElement);

                bool added;
                if (this.lastHash != hash)
                {
                    this.lastHash = hash.Value;
                    added = true;
                }
                else
                {
                    added = false;
                }

                return added;
            }
        }
    }
}
