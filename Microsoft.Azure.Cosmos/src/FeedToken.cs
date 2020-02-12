// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a unit of feed consumption that can be used as unit of parallelism.
    /// </summary>
    [Serializable]
#if PREVIEW
    public
#else
    internal
#endif
    abstract class FeedToken
    {
        /// <summary>
        /// Creates a <see cref="FeedToken"/> from a previously serialized instance.
        /// </summary>
        /// <param name="toStringValue">A string representation obtained from <see cref="FeedToken.ToString()"/>.</param>
        /// <returns>A <see cref="FeedToken"/> instance.</returns>
        public static FeedToken FromString(string toStringValue)
        {
            if (FeedTokenInternal.TryParse(toStringValue, out FeedToken feedToken))
            {
                return feedToken;
            }

            throw new ArgumentOutOfRangeException(nameof(toStringValue), ClientResources.FeedToken_UnknownFormat);
        }

        /// <summary>
        /// Gets a string representation of the current token.
        /// </summary>
        /// <returns>A string representation of the current token.</returns>
        public abstract override string ToString();

        /// <summary>
        /// Overrides the Continuation for an existing FeedToken
        /// </summary>
        /// <remarks>
        /// There is no validation on the format of the Continuation being passed.
        /// </remarks>
        /// <param name="continuationToken">Continuation to be used to update.</param>
        public abstract void UpdateContinuation(string continuationToken);

        /// <summary>
        /// Attempts to split an existing <see cref="FeedToken"/> into individual instances.
        /// </summary>
        /// <remarks>
        /// It is not possible to split all tokens, it depends on the state of the current token itself.
        /// </remarks>
        /// <param name="splitFeedTokens">The resulting list of individual <see cref="FeedToken"/> instances.</param>
        /// <returns>A boolean indicating if split was possible</returns>
        public virtual bool TrySplit(out IEnumerable<FeedToken> splitFeedTokens)
        {
            splitFeedTokens = null;
            return false;
        }
    }
}
