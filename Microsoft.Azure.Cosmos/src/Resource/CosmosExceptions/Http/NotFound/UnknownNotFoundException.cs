// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.NotFound
{
    using System;

    internal sealed class UnknownNotFoundException : NotFoundException
    {
        public UnknownNotFoundException(int subStatusCode)
            : this(subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownNotFoundException(int subStatusCode, string message)
            : this(subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownNotFoundException(int subStatusCode, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
