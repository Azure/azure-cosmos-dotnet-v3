// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;

    internal static class QueryResponseFactory
    {
        private static readonly string EmptyGuidString = Guid.Empty.ToString();

        public static QueryResponseCore CreateFromException(Exception exception)
        {
            QueryResponseCore queryResponseCore;
            if (exception is CosmosException cosmosException)
            {
                queryResponseCore = CreateFromCosmosException(cosmosException);
            }
            else if (exception is Microsoft.Azure.Documents.DocumentClientException documentClientException)
            {
                queryResponseCore = CreateFromDocumentClientException(documentClientException);
            }
            else if (exception is QueryException queryException)
            {
                CosmosException convertedException = queryException.Accept(QueryExceptionConverter.Singleton);
                queryResponseCore = CreateFromCosmosException(convertedException);
            }
            else if (exception is ExceptionWithStackTraceException exceptionWithStackTrace)
            {
                queryResponseCore = QueryResponseFactory.CreateFromExceptionWithStackTrace(exceptionWithStackTrace);
            }
            else
            {
                if (exception.InnerException != null)
                {
                    // retry with the inner exception
                    queryResponseCore = QueryResponseFactory.CreateFromException(exception.InnerException);
                }
                else
                {
                    CosmosException unkownCosmosException = CosmosExceptionFactory.CreateInternalServerErrorException(
                        subStatusCode: default,
                        message: exception.Message,
                        stackTrace: exception.StackTrace,
                        activityId: QueryResponseCore.EmptyGuidString,
                        requestCharge: 0,
                        retryAfter: null,
                        headers: null,
                        trace: NoOpTrace.Singleton,
                        innerException: exception);

                    // Unknown exception type should become a 500
                    queryResponseCore = QueryResponseCore.CreateFailure(
                        statusCode: System.Net.HttpStatusCode.InternalServerError,
                        subStatusCodes: null,
                        cosmosException: unkownCosmosException,
                        requestCharge: 0,
                        activityId: QueryResponseCore.EmptyGuidString);
                }
            }

            return queryResponseCore;
        }

        private static QueryResponseCore CreateFromCosmosException(CosmosException cosmosException)
        {
            QueryResponseCore queryResponseCore = QueryResponseCore.CreateFailure(
                statusCode: cosmosException.StatusCode,
                subStatusCodes: (Microsoft.Azure.Documents.SubStatusCodes)cosmosException.SubStatusCode,
                cosmosException: cosmosException,
                requestCharge: 0,
                activityId: cosmosException.ActivityId);

            return queryResponseCore;
        }

        private static QueryResponseCore CreateFromDocumentClientException(Microsoft.Azure.Documents.DocumentClientException documentClientException)
        {
            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                null);

            QueryResponseCore queryResponseCore = QueryResponseCore.CreateFailure(
                statusCode: documentClientException.StatusCode.GetValueOrDefault(System.Net.HttpStatusCode.InternalServerError),
                subStatusCodes: null,
                cosmosException: cosmosException,
                requestCharge: 0,
                activityId: documentClientException.ActivityId);

            return queryResponseCore;
        }

        private static QueryResponseCore CreateFromExceptionWithStackTrace(ExceptionWithStackTraceException exceptionWithStackTrace)
        {
            // Use the original stack trace from the inner exception.
            if (exceptionWithStackTrace.InnerException is DocumentClientException
                || exceptionWithStackTrace.InnerException is CosmosException)
            {
                return QueryResponseFactory.CreateFromException(exceptionWithStackTrace.InnerException);
            }

            QueryResponseCore queryResponseCore = QueryResponseFactory.CreateFromException(exceptionWithStackTrace.InnerException);
            CosmosException cosmosException = queryResponseCore.CosmosException;

            queryResponseCore = QueryResponseCore.CreateFailure(
                statusCode: queryResponseCore.StatusCode,
                subStatusCodes: queryResponseCore.SubStatusCode,
                cosmosException: CosmosExceptionFactory.Create(
                    cosmosException.StatusCode,
                    cosmosException.SubStatusCode,
                    cosmosException.Message,
                    exceptionWithStackTrace.StackTrace,
                    cosmosException.ActivityId,
                    cosmosException.RequestCharge,
                    cosmosException.RetryAfter,
                    cosmosException.Headers,
                    cosmosException.Trace,
                    cosmosException.Error,
                    cosmosException.InnerException),
                requestCharge: queryResponseCore.RequestCharge,
                activityId: queryResponseCore.ActivityId);

            return queryResponseCore;
        }

        private sealed class QueryExceptionConverter : QueryExceptionVisitor<CosmosException>
        {
            public static readonly QueryExceptionConverter Singleton = new QueryExceptionConverter();

            private QueryExceptionConverter()
            {
            }

            public override CosmosException Visit(MalformedContinuationTokenException malformedContinuationTokenException)
            {
                return CosmosExceptionFactory.CreateBadRequestException(
                    message: malformedContinuationTokenException.Message,
                    stackTrace: malformedContinuationTokenException.StackTrace,
                    innerException: malformedContinuationTokenException);
            }

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException)
            {
                return CosmosExceptionFactory.CreateInternalServerErrorException(
                    message: $"{nameof(CosmosException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    innerException: unexpectedQueryPartitionProviderException);
            }

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException)
            {
                return CosmosExceptionFactory.CreateBadRequestException(
                    message: expectedQueryPartitionProviderException.Message,
                    stackTrace: expectedQueryPartitionProviderException.StackTrace,
                    innerException: expectedQueryPartitionProviderException);
            }
        }
    }
}