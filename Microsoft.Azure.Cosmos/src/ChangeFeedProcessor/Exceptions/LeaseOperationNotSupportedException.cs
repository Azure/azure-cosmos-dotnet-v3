//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Exceptions
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;

    /// <summary>
    /// Exception thrown when a lease operation is not supported for a particular lease type.
    /// </summary>
    [Serializable]
    internal class LeaseOperationNotSupportedException : Exception
    {
        private const string DefaultMessage = "The lease operation is not supported for this lease type.";

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOperationNotSupportedException" /> class.
        /// </summary>
        public LeaseOperationNotSupportedException()
            : base(DefaultMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOperationNotSupportedException" /> class using the specified lease.
        /// </summary>
        /// <param name="lease">Instance of the lease.</param>
        /// <param name="operation">The operation that is not supported.</param>
        public LeaseOperationNotSupportedException(DocumentServiceLease lease, string operation)
            : base($"The operation '{operation}' is not supported for lease type '{lease?.GetType().Name}'. Only EPK-based leases are supported.")
        {
            this.Lease = lease;
            this.Operation = operation;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOperationNotSupportedException" /> class using error message.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        public LeaseOperationNotSupportedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOperationNotSupportedException" /> class using error message and inner exception.
        /// </summary>
        /// <param name="message">The exception error message.</param>
        /// <param name="innerException">The inner exception.</param>
        public LeaseOperationNotSupportedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeaseOperationNotSupportedException"/> class.
        /// </summary>
        /// <param name="info">The SerializationInfo object that holds serialized object data for the exception being thrown.</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
        protected LeaseOperationNotSupportedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        /// <summary>
        /// Gets the lease associated with this exception.
        /// </summary>
        public DocumentServiceLease Lease { get; }

        /// <summary>
        /// Gets the operation that was attempted.
        /// </summary>
        public string Operation { get; }
    }
}
