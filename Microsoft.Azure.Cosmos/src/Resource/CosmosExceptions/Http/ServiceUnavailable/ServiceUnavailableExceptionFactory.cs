// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal static class ServiceUnavailableExceptionFactory
    {
        public static ServiceUnavailableBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new ServiceUnavailableException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)ServiceUnavailableSubStatusCode.InsufficientBindablePartitions:
                    return new InsufficientBindablePartitionsException(cosmosDiagnosticsContext, message, innerException);

                case (int)ServiceUnavailableSubStatusCode.ComputeFederationNotFound:
                    return new ComputeFederationNotFoundException(cosmosDiagnosticsContext, message, innerException);

                case (int)ServiceUnavailableSubStatusCode.OperationPaused:
                    return new OperationPausedException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownServiceUnavailableException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
