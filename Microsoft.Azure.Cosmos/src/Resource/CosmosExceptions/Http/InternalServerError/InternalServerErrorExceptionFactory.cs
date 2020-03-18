// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;

    internal static class InternalServerErrorExceptionFactory
    {
        public static InternalServerErrorException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultInternalServerErrorException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 3001:
                    return new ConfigurationNameNotEmptyException(message, innerException);
                default:
                    return new UnknownInternalServerErrorException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
