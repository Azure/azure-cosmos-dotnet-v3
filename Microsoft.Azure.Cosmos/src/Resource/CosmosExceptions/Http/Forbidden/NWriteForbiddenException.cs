// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class NWriteForbiddenException : ForbiddenException
    {
        public NWriteForbiddenException()
            : this(message: null)
        {
        }

        public NWriteForbiddenException(string message)
            : this(message: message, innerException: null)
        {
        }

        public NWriteForbiddenException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.NWriteForbidden, message: message, innerException: innerException)
        {
        }
    }
}
