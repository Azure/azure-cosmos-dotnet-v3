// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal static class NotFoundExceptionFactory
    {
        public static NotFoundException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultNotFoundException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 1002:
                    return new ReadSessionNotAvailableException(message, innerException);
                case 1003:
                    return new OwnerResourceNotFoundException(message, innerException);
                case 1004:
                    return new ConfigurationNameNotFoundException(message, innerException);
                case 1005:
                    return new ConfigurationPropertyNotFoundException(message, innerException);
                case 1013:
                    return new CollectionCreateInProgressException(message, innerException);
                default:
                    return new UnknownNotFoundException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
