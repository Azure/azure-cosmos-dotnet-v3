// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class DatabaseAccountNotFoundException : ForbiddenException
    {
        public DatabaseAccountNotFoundException()
            : this(message: null)
        {
        }

        public DatabaseAccountNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DatabaseAccountNotFoundException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.DatabaseAccountNotFound, message: message, innerException: innerException)
        {
        }
    }
}
