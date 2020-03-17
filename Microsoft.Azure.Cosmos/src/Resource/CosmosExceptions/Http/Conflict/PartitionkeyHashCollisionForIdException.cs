// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class PartitionkeyHashCollisionForIdException : ConflictException
    {
        public PartitionkeyHashCollisionForIdException()
            : this(message: null)
        {
        }

        public PartitionkeyHashCollisionForIdException(string message)
            : this(message: message, innerException: null)
        {
        }

        public PartitionkeyHashCollisionForIdException(string message, Exception innerException)
            : base(subStatusCode: (int)ConflictSubStatusCode.PartitionkeyHashCollisionForId, message: message, innerException: innerException)
        {
        }
    }
}
