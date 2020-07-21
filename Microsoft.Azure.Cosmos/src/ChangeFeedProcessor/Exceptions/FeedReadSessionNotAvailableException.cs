// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception occurred when all retries on StatusCode.NotFound/SubStatusCode.ReadSessionNotAvaialable are over.
    /// </summary>
    [Serializable]
    internal class FeedReadSessionNotAvailableException : FeedException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeedReadSessionNotAvailableException"/> class using error message and last continuation token.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation"> Request continuation token.</param>
        public FeedReadSessionNotAvailableException(string message, string lastContinuation)
            : base(message, lastContinuation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedReadSessionNotAvailableException" /> class using error message and inner exception.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation">The last known continuation token</param>
        /// <param name="innerException">The inner exception.</param>
        public FeedReadSessionNotAvailableException(string message, string lastContinuation, Exception innerException)
            : base(message, lastContinuation, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedReadSessionNotAvailableException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected FeedReadSessionNotAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}