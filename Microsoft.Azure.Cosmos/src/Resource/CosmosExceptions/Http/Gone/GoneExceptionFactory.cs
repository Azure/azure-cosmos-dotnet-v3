// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal static class GoneExceptionFactory
    {
        public static GoneException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultGoneException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 1000:
                    return new NameCacheIsStaleException(message, innerException);
                case 1002:
                    return new PartitionKeyRangeGoneException(message, innerException);
                case 1007:
                    return new CompletingSplitException(message, innerException);
                case 1008:
                    return new CompletingPartitionMigrationException(message, innerException);
                default:
                    return new UnknownGoneException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
