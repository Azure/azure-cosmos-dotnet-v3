// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal sealed class ComputeFederationNotFoundException : ServiceUnavailableException
    {
        public ComputeFederationNotFoundException()
            : this(message: null)
        {
        }

        public ComputeFederationNotFoundException(string message)
            : this(message: message, innerException: null)
        {
        }

        public ComputeFederationNotFoundException(string message, Exception innerException)
            : base(subStatusCode: (int)ServiceUnavailableSubStatusCode.ComputeFederationNotFound, message: message, innerException: innerException)
        {
        }
    }
}
