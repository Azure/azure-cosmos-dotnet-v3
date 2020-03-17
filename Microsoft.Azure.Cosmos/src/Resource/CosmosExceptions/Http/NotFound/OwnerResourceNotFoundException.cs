// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class OwnerResourceNotFoundException : NotFoundException
    {
        public OwnerResourceNotFoundException()
            : this(message: null)
        {
        }

        public OwnerResourceNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public OwnerResourceNotFoundException(string message, Exception innerException)
            : base(subStatusCode: (int)NotFoundSubStatusCode.OwnerResourceNotFound, message: message, innerException: innerException)
        {
        }
    }
}
