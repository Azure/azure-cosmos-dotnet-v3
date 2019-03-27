//----------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  Licensed under the MIT license.
//----------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeedProcessor.Exceptions
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.ChangeFeedProcessor.LeaseManagement;

    /// <summary>
    /// Exception occurred when lease is lost, that would typically happen when it is taken by another host. Other cases: communication failure, number of retries reached, lease not found.
    /// </summary>
    [Serializable]
    public class LeaseLostException : Exception
    {
        private static readonly string DefaultMessage = "The lease was lost.";

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class.
        /// </summary>
        public LeaseLostException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using the specified lease.
        /// </summary>
        /// <param name="lease">Instance of a lost lease.</param>
        public LeaseLostException(DocumentServiceLease lease)
            : base(DefaultMessage)
        {
            this.Lease = lease;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using error message.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        public LeaseLostException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using error message and inner exception.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LeaseLostException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using the specified lease, and a flag indicating whether lease is gone.
        /// </summary>
        /// <param name="lease">Instance of a lost lease.</param>
        /// <param name="isGone">Whether lease doesn't exist.</param>
        public LeaseLostException(DocumentServiceLease lease, bool isGone)
            : base(DefaultMessage)
        {
            this.Lease = lease;
            this.IsGone = isGone;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using the specified lease, inner exception, and a flag indicating whether lease is gone.
        /// </summary>
        /// <param name="lease">Instance of a lost lease.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="isGone">Whether lease doesn't exist.</param>
        public LeaseLostException(DocumentServiceLease lease, Exception innerException, bool isGone)
            : base(DefaultMessage, innerException)
        {
            this.Lease = lease;
            this.IsGone = isGone;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseLostException" /> class using default values.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected LeaseLostException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            this.Lease = (DocumentServiceLease)info.GetValue("Lease", typeof(DocumentServiceLease));
            this.IsGone = (bool)info.GetValue("IsGone", typeof(bool));
        }

        /// <summary>
        /// Gets the lost lease.
        /// </summary>
        public DocumentServiceLease Lease { get; }

        /// <summary>
        /// Gets a value indicating whether lease doesn't exist.
        /// </summary>
        public bool IsGone { get; }

        /// <summary>
        /// Sets the System.Runtime.Serialization.SerializationInfo with information about the exception.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Lease", this.Lease);
            info.AddValue("IsGone", this.IsGone);
        }
    }
}