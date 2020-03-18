// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.RequestEntityTooLarge
{
    using System;

    internal sealed class UnknownRequestEntityTooLargeException : RequestEntityTooLargeException
    {
        public UnknownRequestEntityTooLargeException(int subStatusCode)
            : this(subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownRequestEntityTooLargeException(int subStatusCode, string message)
            : this(subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownRequestEntityTooLargeException(int subStatusCode, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
