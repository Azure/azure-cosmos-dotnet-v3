// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal abstract class CosmosHttpException : Exception
    {
        protected CosmosHttpException(HttpStatusCode statusCode)
            : this(statusCode, message: null, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, string message)
            : this(statusCode, message: message, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, string message, Exception innerException)
            : base(message: message, innerException: innerException)
        {
            this.StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
