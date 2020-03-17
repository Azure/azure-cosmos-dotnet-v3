// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class PartitionKeyRangeGoneException : GoneException
    {
        public PartitionKeyRangeGoneException()
            : this(message: null)
        {
        }

        public PartitionKeyRangeGoneException(string message)
            : this(message: message, innerException: null)
        {
        }

        public PartitionKeyRangeGoneException(string message, Exception innerException)
            : base(subStatusCode: (int)GoneSubStatusCode.PartitionKeyRangeGone, message: message, innerException: innerException)
        {
        }
    }
}
