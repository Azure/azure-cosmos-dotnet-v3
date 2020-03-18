// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal static class ConflictExceptionFactory
    {
        public static ConflictException Create(
            int? subStatusCode = null,
            string message = null,
            Exception innerException = null)
        {
            if (!subStatusCode.HasValue)
            {
                return new DefaultConflictException(message, innerException);
            }

            switch (subStatusCode.Value)
            {
                case 1006:
                    return new ConflictWithControlPlaneException(message, innerException);
                case 3206:
                    return new DatabaseNameAlreadyExistsException(message, innerException);
                case 3207:
                    return new ConfigurationNameAlreadyExistsException(message, innerException);
                case 3302:
                    return new PartitionkeyHashCollisionForIdException(message, innerException);
                default:
                    return new UnknownConflictException(subStatusCode.Value, message, innerException);
            }
        }
    }
}
