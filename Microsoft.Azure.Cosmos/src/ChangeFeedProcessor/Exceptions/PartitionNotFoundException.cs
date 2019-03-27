//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception occurred when partition wasn't found.
    /// </summary>
    [Serializable]
    public class PartitionNotFoundException : PartitionException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionNotFoundException"/> class using error message and last continuation token.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation"> Request continuation token.</param>
        public PartitionNotFoundException(string message, string lastContinuation)
            : base(message, lastContinuation)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionNotFoundException" /> class using error message and inner exception.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation">The last known continuation token</param>
        /// <param name="innerException">The inner exception.</param>
        public PartitionNotFoundException(string message, string lastContinuation, Exception innerException)
            : base(message, lastContinuation, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PartitionNotFoundException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected PartitionNotFoundException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}