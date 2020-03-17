// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class CollectionCreateInProgressException : NotFoundException
    {
        public CollectionCreateInProgressException()
            : this(message: null)
        {
        }

        public CollectionCreateInProgressException(string message)
            : this(message: message, innerException: null)
        {
        }

        public CollectionCreateInProgressException(string message, Exception innerException)
            : base(subStatusCode: (int)NotFoundSubStatusCode.CollectionCreateInProgress, message: message, innerException: innerException)
        {
        }
    }
}
