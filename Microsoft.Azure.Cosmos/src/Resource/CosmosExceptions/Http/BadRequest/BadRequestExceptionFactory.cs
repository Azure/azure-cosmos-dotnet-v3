// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal static class BadRequestExceptionFactory
    {
        public static BadRequestBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new BadRequestException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)BadRequestSubStatusCode.PartitionKeyMismatch:
                    return new PartitionKeyMismatchException(cosmosDiagnosticsContext, message, innerException);

                case (int)BadRequestSubStatusCode.CrossPartitionQueryNotServable:
                    return new CrossPartitionQueryNotServableException(cosmosDiagnosticsContext, message, innerException);

                case (int)BadRequestSubStatusCode.AnotherOfferReplaceOperationIsInProgress:
                    return new AnotherOfferReplaceOperationIsInProgressException(cosmosDiagnosticsContext, message, innerException);

                case (int)BadRequestSubStatusCode.ScriptCompileError:
                    return new ScriptCompileErrorException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownBadRequestException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
