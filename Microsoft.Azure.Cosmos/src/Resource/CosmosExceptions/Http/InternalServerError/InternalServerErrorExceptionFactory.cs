// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;

    internal static class InternalServerErrorExceptionFactory
    {
        public static InternalServerErrorBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new InternalServerErrorException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)InternalServerErrorSubStatusCode.ConfigurationNameNotEmpty:
                    return new ConfigurationNameNotEmptyException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownInternalServerErrorException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
