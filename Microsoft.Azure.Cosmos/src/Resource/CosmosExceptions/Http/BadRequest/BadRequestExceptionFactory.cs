// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal static class BadRequestExceptionFactory
    {
        public static BadRequestException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultBadRequestException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 1001:
                    return new PartitionKeyMismatchException(message, innerException);
                case 1004:
                    return new CrossPartitionQueryNotServableException(message, innerException);
                case 3205:
                    return new AnotherOfferReplaceOperationIsInProgressException(message, innerException);
                case 65535:
                    return new ScriptCompileErrorException(message, innerException);
                default:
                    return new UnknownBadRequestException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
