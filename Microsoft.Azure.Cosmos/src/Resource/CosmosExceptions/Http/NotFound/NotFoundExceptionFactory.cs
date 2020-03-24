// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal static class NotFoundExceptionFactory
    {
        public static NotFoundBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new NotFoundException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)NotFoundSubStatusCode.ReadSessionNotAvailable:
                    return new ReadSessionNotAvailableException(cosmosDiagnosticsContext, message, innerException);

                case (int)NotFoundSubStatusCode.OwnerResourceNotFound:
                    return new OwnerResourceNotFoundException(cosmosDiagnosticsContext, message, innerException);

                case (int)NotFoundSubStatusCode.ConfigurationNameNotFound:
                    return new ConfigurationNameNotFoundException(cosmosDiagnosticsContext, message, innerException);

                case (int)NotFoundSubStatusCode.ConfigurationPropertyNotFound:
                    return new ConfigurationPropertyNotFoundException(cosmosDiagnosticsContext, message, innerException);

                case (int)NotFoundSubStatusCode.CollectionCreateInProgress:
                    return new CollectionCreateInProgressException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownNotFoundException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
