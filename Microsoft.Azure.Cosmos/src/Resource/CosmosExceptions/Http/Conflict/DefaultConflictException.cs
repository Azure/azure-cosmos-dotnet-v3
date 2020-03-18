// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class DefaultConflictException : ConflictException
    {
        public DefaultConflictException()
            : this(message: null)
        {
        }

        public DefaultConflictException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultConflictException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
