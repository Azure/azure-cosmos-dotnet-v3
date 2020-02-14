// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using System.Diagnostics;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

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
                return QueryResponseFactory.CreateFromException(exceptionWithStackTrace.InnerException);
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
                    CosmosException unkownCosmosException = CosmosExceptionFactory.Create(
                        statusCode: System.Net.HttpStatusCode.InternalServerError,
                        subStatusCode: default,
                        message: exception.Message,
                        stackTrace: new System.Diagnostics.StackTrace(exception),
                        activityId: QueryResponseCore.EmptyGuidString,
                        requestCharge: 0,
                        retryAfter: null,
                        headers: null,
                        diagnosticsContext: null,
                        innerException: exception);

                    // Unknown exception type should become a 500
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

        private sealed class QueryExceptionConverter : QueryExceptionVisitor<CosmosException>
        {
            public static readonly QueryExceptionConverter Singleton = new QueryExceptionConverter();

            private QueryExceptionConverter()
            {
            }

            public override CosmosException Visit(MalformedContinuationTokenException malformedContinuationTokenException)
            {
                return new BadRequestException(
                    message: malformedContinuationTokenException.Message,
                    stackTrace: new StackTrace(malformedContinuationTokenException),
                    innerException: malformedContinuationTokenException);
            }

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException)
            {
                return new InternalServerErrorException(
                    message: $"{nameof(InternalServerErrorException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    innerException: unexpectedQueryPartitionProviderException);
            }

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException)
            {
                return new BadRequestException(
                    message: expectedQueryPartitionProviderException.Message,
                    stackTrace: new StackTrace(expectedQueryPartitionProviderException),
                    innerException: expectedQueryPartitionProviderException);
            }
        }
    }
}