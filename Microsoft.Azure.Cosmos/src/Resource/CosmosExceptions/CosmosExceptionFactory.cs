//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.IO;
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
                    string value = dce.Headers[key];
                    if (value == null)
                    {
                        throw new ArgumentNullException(
                            message: $"{nameof(key)}: {key};",
                            innerException: dce);
                    }

                    headers.Add(key, value);
                }
            }

            HttpStatusCode httpStatusCode;
            if (dce.StatusCode.HasValue)
            {
                httpStatusCode = dce.StatusCode.Value;
            }
            else if (dce.InnerException != null && dce.InnerException is TransportException)
            {
                httpStatusCode = HttpStatusCode.RequestTimeout;
            }
            else
            {
                httpStatusCode = HttpStatusCode.InternalServerError;
            }

            return CosmosExceptionFactory.Create(
                httpStatusCode,
                (int)dce.GetSubStatus(),
                dce.Message,
                dce.StackTrace,
                dce.ActivityId,
                dce.RequestCharge,
                dce.RetryAfter,
                headers,
                diagnosticsContext,
                dce.Error,
                dce.InnerException);
        }

        internal static CosmosException Create(
            HttpStatusCode statusCode,
            RequestMessage requestMessage,
            string errorMessage)
        {
            return CosmosExceptionFactory.Create(
                statusCode: statusCode,
                subStatusCode: default,
                message: errorMessage,
                stackTrace: null,
                activityId: requestMessage?.Headers?.ActivityId,
                requestCharge: 0,
                retryAfter: default,
                headers: requestMessage?.Headers,
                diagnosticsContext: default,
                error: default,
                innerException: default);
        }

        internal static CosmosException Create(
            ResponseMessage responseMessage)
        {
            // If there is no content and there is cosmos exception
            // then use the existing exception
            if (responseMessage.Content == null
                && responseMessage.CosmosException != null)
            {
                return responseMessage.CosmosException;
            }

            // If content was added after the response message
            // creation the exception should be updated.
            string errorMessage = responseMessage.CosmosException?.Message;
            (Error error, string contentMessage) = CosmosExceptionFactory.GetErrorFromStream(responseMessage.Content);
            if (!string.IsNullOrEmpty(contentMessage))
            {
                if (string.IsNullOrEmpty(errorMessage))
                {
                    errorMessage = contentMessage;
                }
                else
                {
                    errorMessage = $"Error Message: {errorMessage}; Content {contentMessage};";
                }
            }

            return CosmosExceptionFactory.Create(
                responseMessage.StatusCode,
                (int)responseMessage.Headers.SubStatusCode,
                errorMessage,
                responseMessage?.CosmosException?.StackTrace,
                responseMessage.Headers.ActivityId,
                responseMessage.Headers.RequestCharge,
                responseMessage.Headers.RetryAfter,
                responseMessage.Headers,
                responseMessage.DiagnosticsContext,
                error,
                responseMessage.CosmosException?.InnerException);
        }

        internal static CosmosException Create(
            DocumentServiceResponse documentServiceResponse,
            Headers responseHeaders,
            RequestMessage requestMessage)
        {
            if (documentServiceResponse == null)
            {
                throw new ArgumentNullException(nameof(documentServiceResponse));
            }

            if (requestMessage == null)
            {
                throw new ArgumentNullException(nameof(requestMessage));
            }

            if (responseHeaders == null)
            {
                responseHeaders = documentServiceResponse.Headers.ToCosmosHeaders();
            }

            (Error error, string errorMessage) = CosmosExceptionFactory.GetErrorFromStream(documentServiceResponse.ResponseBody);

            return CosmosExceptionFactory.Create(
                statusCode: documentServiceResponse.StatusCode,
                subStatusCode: (int)responseHeaders.SubStatusCode,
                message: errorMessage,
                stackTrace: null,
                activityId: responseHeaders.ActivityId,
                requestCharge: responseHeaders.RequestCharge,
                retryAfter: responseHeaders.RetryAfter,
                headers: responseHeaders,
                diagnosticsContext: requestMessage.DiagnosticsContext,
                error: error,
                innerException: null);
        }

        internal static CosmosException Create(
            StoreResponse storeResponse,
            RequestMessage requestMessage)
        {
            if (storeResponse == null)
            {
                throw new ArgumentNullException(nameof(storeResponse));
            }

            if (requestMessage == null)
            {
                throw new ArgumentNullException(nameof(requestMessage));
            }

            (Error error, string errorMessage) = CosmosExceptionFactory.GetErrorFromStream(storeResponse.ResponseBody);
            Headers headers = storeResponse.Headers.ToCosmosHeaders();

            return CosmosExceptionFactory.Create(
                statusCode: storeResponse.StatusCode,
                subStatusCode: (int)headers.SubStatusCode,
                message: errorMessage,
                stackTrace: null,
                activityId: headers.ActivityId,
                requestCharge: headers.RequestCharge,
                retryAfter: headers.RetryAfter,
                headers: headers,
                diagnosticsContext: requestMessage.DiagnosticsContext,
                error: error,
                innerException: null);
        }

        internal static (Error, string) GetErrorFromStream(
            Stream content)
        {
            using (content)
            {
                if (content != null
               && content.CanRead)
                {
                    try
                    {
                        Error error = Documents.Resource.LoadFrom<Error>(content);
                        if (error != null)
                        {
                            // Error format is not consistent across modes
                            return (error, error.ToString());
                        }
                    }
                    catch (Newtonsoft.Json.JsonReaderException)
                    {
                    }

                    // Content is not Json
                    content.Position = 0;
                    using (StreamReader streamReader = new StreamReader(content))
                    {
                        return (null, streamReader.ReadToEnd());
                    }
                }

                return (null, null);
            }
        }

        internal static CosmosException CreateRequestTimeoutException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.RequestTimeout,
                subStatusCode,
                message,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }

        internal static CosmosException CreateThrottledException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                (HttpStatusCode)StatusCodes.TooManyRequests,
                subStatusCode,
                message,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }

        internal static CosmosException CreateNotFoundException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.NotFound,
                subStatusCode,
                message,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }

        internal static CosmosException CreateInternalServerErrorException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.InternalServerError,
                subStatusCode,
                message,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }

        internal static CosmosException CreateBadRequestException(
            string message,
            int subStatusCode = default,
            string stackTrace = default,
            string activityId = default,
            double requestCharge = default,
            TimeSpan? retryAfter = default,
            Headers headers = default,
            CosmosDiagnosticsContext diagnosticsContext = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.BadRequest,
                subStatusCode,
                message,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }

        internal static CosmosException Create(
            HttpStatusCode statusCode,
            int subStatusCode,
            string message,
            string stackTrace,
            string activityId,
            double requestCharge,
            TimeSpan? retryAfter,
            Headers headers,
            CosmosDiagnosticsContext diagnosticsContext,
            Error error,
            Exception innerException)
        {
            return new CosmosException(
                statusCode,
                message,
                subStatusCode,
                stackTrace,
                activityId,
                requestCharge,
                retryAfter,
                headers,
                diagnosticsContext,
                error,
                innerException);
        }
    }
}
