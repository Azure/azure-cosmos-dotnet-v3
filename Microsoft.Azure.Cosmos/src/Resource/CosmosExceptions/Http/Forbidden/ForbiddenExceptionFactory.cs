// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal static class ForbiddenExceptionFactory
    {
        public static ForbiddenException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultForbiddenException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 3:
                    return new NWriteForbiddenException(message, innerException);
                case 1005:
                    return new ProvisionLimitReachedException(message, innerException);
                case 1008:
                    return new DatabaseAccountNotFoundException(message, innerException);
                case 1009:
                    return new RedundantCollectionPutException(message, innerException);
                case 1010:
                    return new SharedThroughputDatabaseQuotaExceededException(message, innerException);
                case 1011:
                    return new SharedThroughputOfferGrowNotNeededException(message, innerException);
                case 1019:
                    return new SharedThroughputDatabaseCollectionCountExceededException(message, innerException);
                case 1020:
                    return new SharedThroughputDatabaseCountExceededException(message, innerException);
                default:
                    return new UnknownForbiddenException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
