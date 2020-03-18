// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;

    internal sealed class DefaultRequestEntityTooLargeException : RequestEntityTooLargeException
    {
        public DefaultRequestEntityTooLargeException()
            : this(message: null)
        {
        }

        public DefaultRequestEntityTooLargeException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultRequestEntityTooLargeException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
