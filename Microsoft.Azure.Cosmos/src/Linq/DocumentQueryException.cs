//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Linq
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents;

    /// <summary> 
    /// Represents an exception from the Azure Cosmos DB service queries.
    /// </summary>
    [System.Serializable]
    internal sealed class DocumentQueryException : DocumentClientException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.Linq.DocumentQueryException"/> class in the Azure Cosmos DB service.</summary>
        /// <param name="message">The exception message.</param>
        public DocumentQueryException(string message)
            : base(message, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.Linq.DocumentQueryException"/> class in the Azure Cosmos DB service.</summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception.</param>
        public DocumentQueryException(string message, Exception innerException)
            : base(message, innerException, null)
        {
        }

#if !NETSTANDARD16
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.Azure.Documents.Linq.DocumentQueryException"/> class.
        /// </summary>
        /// <param name="info">The serialization info.</param>
        /// <param name="context">The streaming context.</param>
        private DocumentQueryException(SerializationInfo info, StreamingContext context)
            : base(info, context, null)
        {
        }
#endif
    }
}
