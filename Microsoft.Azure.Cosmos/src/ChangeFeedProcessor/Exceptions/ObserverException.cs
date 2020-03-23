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
    internal sealed class ObserverException : Exception
    {
        private static readonly string DefaultMessage = "Exception has been thrown by the Observer.";

        /// <summary>
        /// Initializes a new instance of the <see cref="ObserverException" /> class using the specified internal exception.
        /// </summary>
        /// <param name="originalException"><see cref="Exception"/> thrown by the user code.</param>
        public ObserverException(Exception originalException)
            : base(ObserverException.DefaultMessage, originalException)
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