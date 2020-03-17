// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest
{
    using System;

    internal sealed class AnotherOfferReplaceOperationIsInProgressException : BadRequestException
    {
        public AnotherOfferReplaceOperationIsInProgressException()
            : this(message: null)
        {
        }

        public AnotherOfferReplaceOperationIsInProgressException(string message)
            : this(message: message, innerException: null)
        {
        }

        public AnotherOfferReplaceOperationIsInProgressException(string message, Exception innerException)
            : base(subStatusCode: (int)BadRequestSubStatusCode.AnotherOfferReplaceOperationIsInProgress, message: message, innerException: innerException)
        {
        }
    }
}
