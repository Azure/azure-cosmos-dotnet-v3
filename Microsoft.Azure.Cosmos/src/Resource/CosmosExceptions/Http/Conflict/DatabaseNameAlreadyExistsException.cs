// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class DatabaseNameAlreadyExistsException : ConflictException
    {
        public DatabaseNameAlreadyExistsException()
            : this(message: null)
        {
        }

        public DatabaseNameAlreadyExistsException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DatabaseNameAlreadyExistsException(string message, Exception innerException)
            : base(subStatusCode: (int)ConflictSubStatusCode.DatabaseNameAlreadyExists, message: message, innerException: innerException)
        {
        }
    }
}
