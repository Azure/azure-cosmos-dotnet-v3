// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class PartitionKeyMismatchException : BadRequestException
    {
        public PartitionKeyMismatchException()
            : this(message: null)
        {
        }

        public PartitionKeyMismatchException(string message)
            : this(message: message, innerException: null)
        {
        }

        public PartitionKeyMismatchException(string message, Exception innerException)
            : base(subStatusCode: (int)BadRequestSubStatusCode.PartitionKeyMismatch, message: message, innerException: innerException)
        {
        }
    }
}
