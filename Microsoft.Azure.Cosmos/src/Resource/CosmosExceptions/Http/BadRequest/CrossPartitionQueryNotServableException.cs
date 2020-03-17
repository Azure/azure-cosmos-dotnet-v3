// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class CrossPartitionQueryNotServableException : BadRequestException
    {
        public CrossPartitionQueryNotServableException()
            : this(message: null)
        {
        }

        public CrossPartitionQueryNotServableException(string message)
            : this(message: message, innerException: null)
        {
        }

        public CrossPartitionQueryNotServableException(string message, Exception innerException)
            : base(subStatusCode: (int)BadRequestSubStatusCode.CrossPartitionQueryNotServable, message: message, innerException: innerException)
        {
        }
    }
}
