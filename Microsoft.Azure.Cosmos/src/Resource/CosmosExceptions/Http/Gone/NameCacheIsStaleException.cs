// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class NameCacheIsStaleException : GoneException
    {
        public NameCacheIsStaleException()
            : this(message: null)
        {
        }

        public NameCacheIsStaleException(string message)
            : this(message: message, innerException: null)
        {
        }

        public NameCacheIsStaleException(string message, Exception innerException)
            : base(subStatusCode: (int)GoneSubStatusCode.NameCacheIsStale, message: message, innerException: innerException)
        {
        }
    }
}
