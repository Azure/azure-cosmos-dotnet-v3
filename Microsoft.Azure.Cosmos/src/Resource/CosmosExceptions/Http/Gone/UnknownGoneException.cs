// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Gone
{
    using System;

    internal sealed class UnknownGoneException : GoneException
    {
        public UnknownGoneException(int subStatusCode)
            : this(subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownGoneException(int subStatusCode, string message)
            : this(subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownGoneException(int subStatusCode, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
