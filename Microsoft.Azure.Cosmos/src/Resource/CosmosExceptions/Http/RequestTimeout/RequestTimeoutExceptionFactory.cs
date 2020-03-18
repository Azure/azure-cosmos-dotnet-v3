// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestTimeout
{
    using System;

    internal static class RequestTimeoutExceptionFactory
    {
        public static RequestTimeoutException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultRequestTimeoutException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                default:
                    return new UnknownRequestTimeoutException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
