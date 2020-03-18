// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestRateTooLarge
{
    using System;

    internal static class RequestRateTooLargeExceptionFactory
    {
        public static RequestRateTooLargeException Create(
            TimeSpan retryAfter,
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultRequestRateTooLargeException(retryAfter, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                default:
                    return new UnknownRequestRateTooLargeException(retryAfter, subStatusCode.Value, message, innerException);
            }
        }
    }
}
