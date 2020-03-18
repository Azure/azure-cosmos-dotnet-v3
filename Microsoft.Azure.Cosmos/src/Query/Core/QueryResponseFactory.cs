// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.ServiceModel.Channels;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.BadRequest;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions.Http.InternalServerError;
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
                    // Unknown exception type should become a 500
                    CosmosException unkownCosmosException = InternalServerErrorExceptionFactory.Create(
                        message: "Query encountered unknown exception.",
                        innerException: exception);

                    queryResponseCore = QueryResponseCore.CreateFailure(
                        statusCode: System.Net.HttpStatusCode.InternalServerError,
                        subStatusCodes: null,
                        cosmosException: unkownCosmosException,
                        requestCharge: 0,
                        activityId: QueryResponseCore.EmptyGuidString,
                        diagnostics: QueryResponseCore.EmptyDiagnostics);
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
                activityId: cosmosException.ActivityId,
                diagnostics: QueryResponseCore.EmptyDiagnostics);

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
                activityId: documentClientException.ActivityId,
                diagnostics: QueryResponseCore.EmptyDiagnostics);

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
#pragma warning disable CS0612 // Type or member is obsolete
                cosmosException: CosmosExceptionFactory.Create(
                    cosmosException.StatusCode,
                    cosmosException.SubStatusCode,
                    cosmosException.Message,
                    exceptionWithStackTrace.StackTrace,
                    cosmosException.ActivityId,
                    cosmosException.RequestCharge,
                    cosmosException.RetryAfter,
                    cosmosException.Headers,
                    cosmosException.DiagnosticsContext,
                    cosmosException.Error,
                    cosmosException.InnerException),
#pragma warning restore CS0612 // Type or member is obsolete
                requestCharge: queryResponseCore.RequestCharge,
                activityId: queryResponseCore.ActivityId,
                diagnostics: queryResponseCore.Diagnostics);

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
                return BadRequestExceptionFactory.Create(
                    message: $"{nameof(CosmosException)} created due to {nameof(MalformedContinuationTokenException)}.",
                    innerException: malformedContinuationTokenException);
            }

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException)
            {
                return InternalServerErrorExceptionFactory.Create(
                    message: $"{nameof(CosmosException)} created due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    innerException: unexpectedQueryPartitionProviderException);
            }

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException)
            {
                return BadRequestExceptionFactory.Create(
                    message: $"{nameof(CosmosException)} created due to {nameof(ExpectedQueryPartitionProviderException)}.",
                    innerException: expectedQueryPartitionProviderException);
            }
        }
    }
}