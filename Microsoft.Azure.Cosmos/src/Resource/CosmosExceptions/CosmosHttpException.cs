// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Net;

    internal abstract class CosmosHttpException : CosmosException
    {
        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode)
            : this(statusCode, subStatusCode, message: null, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode, string message)
            : this(statusCode, subStatusCode, message: message, innerException: null)
        {
        }

        protected CosmosHttpException(HttpStatusCode statusCode, int subStatusCode, string message, Exception innerException)
#pragma warning disable CS0618 // Type or member is obsolete
            : base(statusCode: statusCode, subStatusCode: subStatusCode, message: message, activityId: null, requestCharge: 0, innerException: innerException)
#pragma warning restore CS0618 // Type or member is obsolete
        {
        }
    }
}