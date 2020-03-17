// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class SharedThroughputOfferGrowNotNeededException : ForbiddenException
    {
        public SharedThroughputOfferGrowNotNeededException()
            : this(message: null)
        {
        }

        public SharedThroughputOfferGrowNotNeededException(string message)
            : this(message: message, innerException: null)
        {
        }

        public SharedThroughputOfferGrowNotNeededException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.SharedThroughputOfferGrowNotNeeded, message: message, innerException: innerException)
        {
        }
    }
}
