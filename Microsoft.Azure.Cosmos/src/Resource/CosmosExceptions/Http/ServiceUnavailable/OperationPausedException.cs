// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.ServiceUnavailable
{
    using System;

    internal sealed class OperationPausedException : ServiceUnavailableException
    {
        public OperationPausedException()
            : this(message: null)
        {
        }

        public OperationPausedException(string message)
            : this(message: message, innerException: null)
        {
        }

        public OperationPausedException(string message, Exception innerException)
            : base(subStatusCode: (int)ServiceUnavailableSubStatusCode.OperationPaused, message: message, innerException: innerException)
        {
        }
    }
}
