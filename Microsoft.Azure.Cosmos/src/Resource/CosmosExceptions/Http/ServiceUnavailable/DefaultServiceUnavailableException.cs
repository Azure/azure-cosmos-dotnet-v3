// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal sealed class DefaultServiceUnavailableException : ServiceUnavailableException
    {
        public DefaultServiceUnavailableException()
            : this(message: null)
        {
        }

        public DefaultServiceUnavailableException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultServiceUnavailableException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
