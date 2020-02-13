// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;

    internal sealed class BadRequestException : CosmosHttpException
    {
        public BadRequestException()
            : base(statusCode: HttpStatusCode.BadRequest, message: null)
        {
        }

        public BadRequestException(string message)
            : base(statusCode: HttpStatusCode.BadRequest, message: message)
        {
        }

        public BadRequestException(string message, Exception innerException)
            : base(statusCode: HttpStatusCode.BadRequest, message: message, innerException: innerException)
        {
        }

        internal BadRequestException(
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
            : base(HttpStatusCode.BadRequest,
             subStatusCode,
             message,
             stackTrace,
             activityId,
             requestCharge,
             retryAfter,
             headers,
             diagnosticsContext,
             innerException)
        {
        }
    }
}
