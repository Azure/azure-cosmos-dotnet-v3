//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// Exception occurred when an operation in an IChangeFeedObserver is running and throws by user code
    /// </summary>
    [Serializable]

#if PREVIEW
    public
#else
    internal
#endif
    class ChangeFeedProcessorUserException : Exception
    {
        private static readonly string DefaultMessage = "Exception has been thrown by the change feed processor delegate.";

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeFeedProcessorUserException " /> class using the specified internal exception.
        /// </summary>
        /// <param name="originalException"><see cref="Exception"/> thrown by the user code.</param>
        public ChangeFeedProcessorUserException(Exception originalException)
            : base(ChangeFeedProcessorUserException.DefaultMessage, originalException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ChangeFeedProcessorUserException " /> for serialization purposes.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected ChangeFeedProcessorUserException(SerializationInfo info, StreamingContext context)
            : this((Exception)info.GetValue("InnerException", typeof(Exception)))
        {
        }

        /// <summary>
        /// Sets the System.Runtime.Serialization.SerializationInfo with information about the exception.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }
    }
}