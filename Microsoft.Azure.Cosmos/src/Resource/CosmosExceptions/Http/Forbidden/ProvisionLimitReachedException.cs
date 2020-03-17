// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class ProvisionLimitReachedException : ForbiddenException
    {
        public ProvisionLimitReachedException()
            : this(message: null)
        {
        }

        public ProvisionLimitReachedException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ProvisionLimitReachedException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.ProvisionLimitReached, message: message, innerException: innerException)
        {
        }
    }
}
