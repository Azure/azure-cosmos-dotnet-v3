// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class DefaultBadRequestException : BadRequestException
    {
        public DefaultBadRequestException()
            : this(message: null)
        {
        }

        public DefaultBadRequestException(string message)
            : this(message: message, innerException: null)
        {
        }

        public DefaultBadRequestException(string message, Exception innerException)
            : base(subStatusCode: 0, message: message, innerException: innerException)
        {
        }
    }
}
