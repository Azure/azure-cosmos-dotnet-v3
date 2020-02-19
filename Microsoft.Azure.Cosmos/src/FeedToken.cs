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
        /// Attempts to scale an existing <see cref="FeedToken"/> into more granular FeedTokens if-possible
        /// </summary>
        /// <remarks>
        /// It is not always possible to scale a token, but when it is, the list of resulting tokens is returned.
        /// Each token then can be used to start its own read process in parallel and the current token that origincated the scale can be discarded.
        /// The amount of tokens returned is up to <paramref name="maxTokens"/>, but it could be less.
        /// </remarks>
        /// <param name="maxTokens">Defines a maximum amount of tokens to be returned.</param>
        /// <returns>The resulting list of individual <see cref="FeedToken"/> instances.</returns>
        public abstract IReadOnlyList<FeedToken> Scale(int? maxTokens = null);
    }
}
