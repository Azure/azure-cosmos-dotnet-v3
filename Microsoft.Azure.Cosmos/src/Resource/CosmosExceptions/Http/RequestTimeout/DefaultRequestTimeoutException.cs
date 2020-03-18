// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;

    internal sealed class DefaultRequestTimeoutException : RequestTimeoutException
    {
        public DefaultRequestTimeoutException()
            : this(message: null)
        {
        }

        public DefaultRequestTimeoutException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultRequestTimeoutException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
