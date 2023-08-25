﻿//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception occurred during feed processing because of a partition gone.
    /// </summary>
    [Serializable]
    internal class FeedRangeGoneException : FeedException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeedRangeGoneException"/> class using error message and last continuation token.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation"> Request continuation token.</param>
        public FeedRangeGoneException(string message, string lastContinuation)
            : base(message, lastContinuation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedRangeGoneException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected FeedRangeGoneException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}