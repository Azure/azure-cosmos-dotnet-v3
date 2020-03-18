// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class DefaultNotFoundException : NotFoundException
    {
        public DefaultNotFoundException()
            : this(message: null)
        {
        }

        public DefaultNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultNotFoundException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
