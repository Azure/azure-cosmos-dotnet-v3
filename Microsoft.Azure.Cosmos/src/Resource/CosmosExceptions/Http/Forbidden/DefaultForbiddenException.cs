// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class DefaultForbiddenException : ForbiddenException
    {
        public DefaultForbiddenException()
            : this(message: null)
        {
        }

        public DefaultForbiddenException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultForbiddenException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
