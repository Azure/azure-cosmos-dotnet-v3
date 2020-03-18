// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal static class ServiceUnavailableExceptionFactory
    {
        public static ServiceUnavailableException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultServiceUnavailableException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 1007:
                    return new InsufficientBindablePartitionsException(message, innerException);
                case 1012:
                    return new ComputeFederationNotFoundException(message, innerException);
                case 9001:
                    return new OperationPausedException(message, innerException);
                default:
                    return new UnknownServiceUnavailableException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
