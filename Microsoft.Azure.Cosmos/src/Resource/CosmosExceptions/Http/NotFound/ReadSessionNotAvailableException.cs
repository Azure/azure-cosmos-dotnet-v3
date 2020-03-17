// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class ReadSessionNotAvailableException : NotFoundException
    {
        public ReadSessionNotAvailableException()
            : this(message: null)
        {
        }

        public ReadSessionNotAvailableException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ReadSessionNotAvailableException(string message, Exception innerException)
            : base(subStatusCode: (int)NotFoundSubStatusCode.ReadSessionNotAvailable, message: message, innerException: innerException)
        {
        }
    }
}
