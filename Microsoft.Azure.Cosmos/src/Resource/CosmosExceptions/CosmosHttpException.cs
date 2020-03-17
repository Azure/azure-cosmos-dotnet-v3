// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal abstract class CosmosHttpException : CosmosHttpWithSubstatusCodeException
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
#pragma warning disable CS0618 // Type or member is obsolete
            : base(statusCode: statusCode, message: message, subStatusCode: 0, innerException: innerException)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}