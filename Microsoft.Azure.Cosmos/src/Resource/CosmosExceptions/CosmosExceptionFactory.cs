//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Diagnostics;
    using System.Net;
    using Microsoft.Azure.Documents;

    internal static class CosmosExceptionFactory
    {
        internal static CosmosException Create(
            DocumentClientException dce,
            CosmosDiagnosticsContext diagnosticsContext)
        {
            Headers headers = new Headers();
            if (dce.Headers != null)
            {
                foreach (string key in dce.Headers)
                {
                    headers.Add(key, dce.Headers[key]);
                }
            }

            return CosmosExceptionFactory.Create(
                dce.StatusCode ?? HttpStatusCode.InternalServerError,
                (int)dce.GetSubStatus(),
                dce.Message,
                new StackTrace(dce),
                dce.ActivityId,
                dce.RequestCharge,
                dce.RetryAfter,
                headers,
                diagnosticsContext,
                dce.InnerException);
        }

        public static CosmosException Create(
            HttpStatusCode statusCode,
            int subStatusCode,
            string message,
            StackTrace stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Exception innerException)
        {
            switch (statusCode)
            {
                case HttpStatusCode.InternalServerError:
                    return new InternalServerErrorException(
                         subStatusCode,
                         message,
                         stackTrace,
                         activityId,
                         requestCharge,
                         retryAfter,
                         headers,
                         diagnosticsContext,
                         innerException);
                case HttpStatusCode.BadRequest:
                    return new BadRequestException(
                         subStatusCode,
                         message,
                         stackTrace,
                         activityId,
                         requestCharge,
                         retryAfter,
                         headers,
                         diagnosticsContext,
                         innerException);
                default:
                    return new CosmosException(
                        statusCode,
                        subStatusCode,
                        message,
                        stackTrace,
                        activityId,
                        requestCharge,
                        retryAfter,
                        headers,
                        diagnosticsContext,
                        innerException);
            }
        }
    }
}
