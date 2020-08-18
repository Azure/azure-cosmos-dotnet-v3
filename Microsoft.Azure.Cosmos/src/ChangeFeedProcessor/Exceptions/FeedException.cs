//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception occurred during feed processing because of a split.
    /// </summary>
    [Serializable]
    internal abstract class FeedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FeedException"/> class using error message and last continuation token.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation"> Request continuation token.</param>
        public FeedException(string message, string lastContinuation)
            : base(message)
        {
            this.LastContinuation = lastContinuation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedException" /> class using error message and inner exception.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="lastContinuation">Request continuation token.</param>
        /// <param name="innerException">The inner exception.</param>
        protected FeedException(string message, string lastContinuation, Exception innerException)
            : base(message, innerException)
        {
            this.LastContinuation = lastContinuation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FeedException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected FeedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.LastContinuation = (string)info.GetValue("LastContinuation", typeof(string));
        }

        /// <summary>
        /// Gets the value of request continuation token.
        /// </summary>
        public string LastContinuation { get; }

        /// <summary>
        /// Sets the System.Runtime.Serialization.SerializationInfo with information about the exception.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("LastContinuation", this.LastContinuation);
        }
    }
}