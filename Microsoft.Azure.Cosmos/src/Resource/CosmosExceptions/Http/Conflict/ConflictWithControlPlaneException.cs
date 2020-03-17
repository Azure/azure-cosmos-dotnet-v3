// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Conflict
{
    using System;

    internal sealed class ConflictWithControlPlaneException : ConflictException
    {
        public ConflictWithControlPlaneException()
            : this(message: null)
        {
        }

        public ConflictWithControlPlaneException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ConflictWithControlPlaneException(string message, Exception innerException)
            : base(subStatusCode: (int)ConflictSubStatusCode.ConflictWithControlPlane, message: message, innerException: innerException)
        {
        }
    }
}
