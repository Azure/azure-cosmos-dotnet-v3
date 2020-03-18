// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal sealed class UnknownCosmosHttpException : CosmosHttpException
    {
        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode)
            : this(statusCode, subStatusCode, message: null, innerException: null)
        {
        }

        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode, string message)
            : this(statusCode, subStatusCode, message: message, innerException: null)
        {
        }

        public UnknownCosmosHttpException(HttpStatusCode statusCode, int subStatusCode, string message, Exception innerException)
            : base(statusCode: statusCode, subStatusCode: subStatusCode, message: message, innerException: innerException)
        {
        }
    }
}
