// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal static class ForbiddenExceptionFactory
    {
        public static ForbiddenBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new ForbiddenException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)ForbiddenSubStatusCode.NWriteForbidden:
                    return new NWriteForbiddenException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.ProvisionLimitReached:
                    return new ProvisionLimitReachedException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.DatabaseAccountNotFound:
                    return new DatabaseAccountNotFoundException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.RedundantCollectionPut:
                    return new RedundantCollectionPutException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.SharedThroughputDatabaseQuotaExceeded:
                    return new SharedThroughputDatabaseQuotaExceededException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.SharedThroughputOfferGrowNotNeeded:
                    return new SharedThroughputOfferGrowNotNeededException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.SharedThroughputDatabaseCollectionCountExceeded:
                    return new SharedThroughputDatabaseCollectionCountExceededException(cosmosDiagnosticsContext, message, innerException);

                case (int)ForbiddenSubStatusCode.SharedThroughputDatabaseCountExceeded:
                    return new SharedThroughputDatabaseCountExceededException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownForbiddenException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
