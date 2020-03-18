// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;

    internal sealed class DefaultInternalServerErrorException : InternalServerErrorException
    {
        public DefaultInternalServerErrorException()
            : this(message: null)
        {
        }

        public DefaultInternalServerErrorException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultInternalServerErrorException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
