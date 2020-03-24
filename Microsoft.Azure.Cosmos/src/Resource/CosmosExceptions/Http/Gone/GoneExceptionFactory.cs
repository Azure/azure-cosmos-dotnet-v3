// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal static class GoneExceptionFactory
    {
        public static GoneBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new GoneException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)GoneSubStatusCode.NameCacheIsStale:
                    return new NameCacheIsStaleException(cosmosDiagnosticsContext, message, innerException);

                case (int)GoneSubStatusCode.PartitionKeyRangeGone:
                    return new PartitionKeyRangeGoneException(cosmosDiagnosticsContext, message, innerException);

                case (int)GoneSubStatusCode.CompletingSplit:
                    return new CompletingSplitException(cosmosDiagnosticsContext, message, innerException);

                case (int)GoneSubStatusCode.CompletingPartitionMigration:
                    return new CompletingPartitionMigrationException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownGoneException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
