// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError
{
    using System;

    internal sealed class UnknownInternalServerErrorException : InternalServerErrorException
    {
        public UnknownInternalServerErrorException(int subStatusCode)
            : this(subStatusCode: subStatusCode, message: null)
        {
        }

        public UnknownInternalServerErrorException(int subStatusCode, string message)
            : this(subStatusCode: subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownInternalServerErrorException(int subStatusCode, string message, Exception innerException)
            : base(subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
