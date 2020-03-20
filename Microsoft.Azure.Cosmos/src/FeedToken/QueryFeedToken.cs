// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a unit of query consumption that can be used as unit of parallelism.
    /// </summary>
    [Serializable]
#if PREVIEW
    public
#else
    internal
#endif
    abstract class QueryFeedToken
    {
        /// <summary>
        /// Creates a <see cref="QueryFeedToken"/> from a previously serialized instance.
        /// </summary>
        /// <param name="toStringValue">A string representation obtained from <see cref="QueryFeedToken.ToString()"/>.</param>
        /// <returns>A <see cref="QueryFeedToken"/> instance.</returns>
        public static QueryFeedToken FromString(string toStringValue)
        {
            if (QueryFeedTokenInternal.TryParse(toStringValue, out QueryFeedToken feedToken))
            {
                return feedToken;
            }

            throw new ArgumentOutOfRangeException(nameof(toStringValue), string.Format(ClientResources.FeedToken_CannotParse, toStringValue));
        }

        /// <summary>
        /// Gets a string representation of the current token.
        /// </summary>
        /// <returns>A string representation of the current token.</returns>
        public abstract override string ToString();

        /// <summary>
        /// Attempts to scale an existing <see cref="QueryFeedToken"/> into more granular FeedTokens if-possible
        /// </summary>
        /// <remarks>
        /// It is not always possible to scale a token, but when it is, the list of resulting tokens is returned.
        /// Each token then can be used to start its own read process in parallel and the current token that originated the scale can be discarded.
        /// </remarks>
        /// <returns>The resulting list of individual <see cref="QueryFeedToken"/> instances.</returns>
        public abstract IReadOnlyList<QueryFeedToken> Scale();
    }
}
