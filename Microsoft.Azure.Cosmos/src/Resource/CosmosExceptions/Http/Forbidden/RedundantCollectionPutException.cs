// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// This is generated code:

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.Forbidden
{
    using System;

    internal sealed class RedundantCollectionPutException : ForbiddenException
    {
        public RedundantCollectionPutException()
            : this(message: null)
        {
        }

        public RedundantCollectionPutException(string message)
            : this(message: message, innerException: null)
        {
        }

        public RedundantCollectionPutException(string message, Exception innerException)
            : base(subStatusCode: (int)ForbiddenSubStatusCode.RedundantCollectionPut, message: message, innerException: innerException)
        {
        }
    }
}
