//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.IO;
    using System.Linq;
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
            bool isPureHttpException =
                (stackTrace == default) &&
                (activityId == default) &&
                (requestCharge == 0) &&
                (retryAfter == default) &&
                ((headers == default) || (headers.AllKeys().Length == 0)) &&
                ((diagnosticsContext == default) || !diagnosticsContext.Any()) &&
                (error == default) &&
                (innerException == default);

            CosmosException cosmosException;
            if (isPureHttpException)
            {
                cosmosException = CosmosHttpExceptionFactory.Create(
                    statusCode,
                    subStatusCode,
                    message,
                    innerException);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete
                // We are defaulting to this constructor, since we don't want to break callers.
                // In the future CosmosException will be purely abstract with no members and the user will just have to cast down to get extra info like requestCharge.
                cosmosException = new CosmosException(
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
#pragma warning restore CS0618 // Type or member is obsolete
            }

            return cosmosException;
        }

        private static (Error, string) GetErrorFromStream(
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
    }
}
