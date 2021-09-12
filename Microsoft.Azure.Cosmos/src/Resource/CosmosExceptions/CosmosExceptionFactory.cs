//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.CosmosExceptions
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class CosmosExceptionFactory
    {
        internal static CosmosException Create(
            DocumentClientException dce,
            ITrace trace)
        {
            Headers headers = dce.Headers == null ? new Headers() : new Headers(dce.Headers);

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
                dce.Message,
                dce.StackTrace,
                headers,
                trace,
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
                message: errorMessage,
                stackTrace: null,
                headers: requestMessage?.Headers,
                trace: NoOpTrace.Singleton,
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
                errorMessage = string.IsNullOrEmpty(errorMessage) ? contentMessage : $"Error Message: {errorMessage}; Content {contentMessage};";
            }

            return CosmosExceptionFactory.Create(
                responseMessage.StatusCode,
                errorMessage,
                responseMessage?.CosmosException?.StackTrace,
                responseMessage.Headers,
                responseMessage.Trace,
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
                responseHeaders = new Headers(documentServiceResponse.Headers);
            }

            (Error error, string errorMessage) = CosmosExceptionFactory.GetErrorFromStream(documentServiceResponse.ResponseBody);

            return CosmosExceptionFactory.Create(
                statusCode: documentServiceResponse.StatusCode,
                message: errorMessage,
                stackTrace: null,
                headers: responseHeaders,
                trace: requestMessage.Trace,
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
            Headers headers = new Headers(storeResponse.Headers);

            return CosmosExceptionFactory.Create(
                statusCode: storeResponse.StatusCode,
                message: errorMessage,
                stackTrace: null,
                headers: headers,
                trace: requestMessage.Trace,
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
                    using (StreamReader streamReader = new StreamReader(content))
                    {
                        string errorContent = streamReader.ReadToEnd();
                        try
                        {
                            JObject errorObj = JObject.Parse(errorContent);
                            Error error = errorObj.ToObject<Error>();
                            if (error != null)
                            {
                                StringBuilder message = new StringBuilder();
                                foreach (var err in errorObj)
                                {
                                    message
                                        .Append(Environment.NewLine)
                                        .Append(err.Key)
                                        .Append(" : ")
                                        .Append(err.Value);
                                }
                                message.Append(Environment.NewLine);
                                // Error format is not consistent across modes
                                return (error, message.ToString());
                            }
                        }
                        catch (Newtonsoft.Json.JsonReaderException)
                        {
                        }

                        // Content is not Json
                        content.Position = 0;
                        return (null, errorContent);
                    }
                }

                return (null, null);
            }
        }

        internal static CosmosException CreateRequestTimeoutException(
            string message,
            Headers headers,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.RequestTimeout,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException CreateThrottledException(
            string message,
            Headers headers,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                (HttpStatusCode)StatusCodes.TooManyRequests,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException CreateNotFoundException(
            string message,
            Headers headers,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.NotFound,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException CreateInternalServerErrorException(
            string message,
            Headers headers,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.InternalServerError,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException CreateBadRequestException(
            string message,
            Headers headers,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default,
            Exception innerException = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.BadRequest,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException CreateUnauthorizedException(
            string message,
            Headers headers,
            Exception innerException,
            string stackTrace = default,
            ITrace trace = default,
            Error error = default)
        {
            return CosmosExceptionFactory.Create(
                HttpStatusCode.Unauthorized,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }

        internal static CosmosException Create(
            HttpStatusCode statusCode,
            string message,
            string stackTrace,
            Headers headers,
            ITrace trace,
            Error error,
            Exception innerException)
        {
            return new CosmosException(
                statusCode,
                message,
                stackTrace,
                headers,
                trace,
                error,
                innerException);
        }
    }
}
