// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class CompletingPartitionMigrationException : GoneException
    {
        public CompletingPartitionMigrationException()
            : this(message: null)
        {
        }

        public CompletingPartitionMigrationException(string message)
            : this(message: message, innerException: null)
        {
        }

        public CompletingPartitionMigrationException(string message, Exception innerException)
            : base(subStatusCode: (int)GoneSubStatusCode.CompletingPartitionMigration, message: message, innerException: innerException)
        {
        }
    }
}
