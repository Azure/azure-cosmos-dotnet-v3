// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class SharedThroughputDatabaseCollectionCountExceededException : ForbiddenException
    {
        public SharedThroughputDatabaseCollectionCountExceededException()
            : this(message: null)
        {
        }

        public SharedThroughputDatabaseCollectionCountExceededException(string message)
            : this(message: message, innerException: null)
        {
        }

        public SharedThroughputDatabaseCollectionCountExceededException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.SharedThroughputDatabaseCollectionCountExceeded, message: message, innerException: innerException)
        {
        }
    }
}
