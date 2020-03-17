// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class SharedThroughputDatabaseCountExceededException : ForbiddenException
    {
        public SharedThroughputDatabaseCountExceededException()
            : this(message: null)
        {
        }

        public SharedThroughputDatabaseCountExceededException(string message)
            : this(message: message, innerException: null)
        {
        }

        public SharedThroughputDatabaseCountExceededException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.SharedThroughputDatabaseCountExceeded, message: message, innerException: innerException)
        {
        }
    }
}
