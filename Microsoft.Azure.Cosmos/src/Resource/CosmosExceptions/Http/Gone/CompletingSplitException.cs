// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class CompletingSplitException : GoneException
    {
        public CompletingSplitException()
            : this(message: null)
        {
        }

        public CompletingSplitException(string message)
            : this(message: message, innerException: null)
        {
        }

        public CompletingSplitException(string message, Exception innerException)
            : base(subStatusCode: (int)GoneSubStatusCode.CompletingSplit, message: message, innerException: innerException)
        {
        }
    }
}
