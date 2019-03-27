//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions
{
    using System;
    using System.Runtime.Serialization;

    ///// Exception occurred when an operation in an <see cref="IChangeFeedObserver"/> is running and throws by user code


    /// <summary>
    /// Exception occurred when an operation in an IChangeFeedObserver is running and throws by user code
    /// </summary>
    [Serializable]
    public class ObserverException : Exception
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
        /// Initializes a new instance of the <see cref="ObserverException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected ObserverException(SerializationInfo info, StreamingContext context)
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