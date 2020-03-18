// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class DefaultGoneException : GoneException
    {
        public DefaultGoneException()
            : this(message: null)
        {
        }

        public DefaultGoneException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultGoneException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
