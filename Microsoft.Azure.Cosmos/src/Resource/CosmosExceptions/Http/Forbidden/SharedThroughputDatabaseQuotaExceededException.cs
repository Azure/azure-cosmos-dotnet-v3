// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class SharedThroughputDatabaseQuotaExceededException : ForbiddenException
    {
        public SharedThroughputDatabaseQuotaExceededException()
            : this(message: null)
        {
        }

        public SharedThroughputDatabaseQuotaExceededException(string message)
            : this(message: message, innerException: null)
        {
        }

        public SharedThroughputDatabaseQuotaExceededException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.SharedThroughputDatabaseQuotaExceeded, message: message, innerException: innerException)
        {
        }
    }
}
