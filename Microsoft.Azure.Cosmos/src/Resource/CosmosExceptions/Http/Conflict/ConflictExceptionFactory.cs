// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is auto-generated code. Modify: HttpExceptionCodeGenerator.tt: 198

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal static class ConflictExceptionFactory
    {
        public static ConflictBaseException Create(
            int? subStatusCode = null,
            CosmosDiagnosticsContext cosmosDiagnosticsContext = null,
            string message = null,
            Exception innerException = null)
        {
            cosmosDiagnosticsContext = cosmosDiagnosticsContext ?? new CosmosDiagnosticsContextCore();
            if (!subStatusCode.HasValue)
            {
                return new ConflictException(cosmosDiagnosticsContext, message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case (int)ConflictSubStatusCode.ConflictWithControlPlane:
                    return new ConflictWithControlPlaneException(cosmosDiagnosticsContext, message, innerException);

                case (int)ConflictSubStatusCode.DatabaseNameAlreadyExists:
                    return new DatabaseNameAlreadyExistsException(cosmosDiagnosticsContext, message, innerException);

                case (int)ConflictSubStatusCode.ConfigurationNameAlreadyExists:
                    return new ConfigurationNameAlreadyExistsException(cosmosDiagnosticsContext, message, innerException);

                case (int)ConflictSubStatusCode.PartitionkeyHashCollisionForId:
                    return new PartitionkeyHashCollisionForIdException(cosmosDiagnosticsContext, message, innerException);

                default:
                    return new UnknownConflictException(subStatusCode.Value, cosmosDiagnosticsContext, message, innerException);
            }
        }
    }
}
