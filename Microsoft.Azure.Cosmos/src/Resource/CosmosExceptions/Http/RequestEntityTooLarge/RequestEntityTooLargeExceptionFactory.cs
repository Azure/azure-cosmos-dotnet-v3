// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;

    internal static class RequestEntityTooLargeExceptionFactory
    {
        public static RequestEntityTooLargeException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultRequestEntityTooLargeException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                default:
                    return new UnknownRequestEntityTooLargeException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
