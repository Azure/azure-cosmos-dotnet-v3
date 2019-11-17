// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;

    internal static class QueryResponseFactory
    {
        private static readonly string EmptyGuidString = Guid.Empty.ToString();

        public static QueryResponseCore CreateFromException(Exception exception)
        {
            // Get the inner most exception
            while (exception.InnerException != null) exception = exception.InnerException;

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
            else
            {
                // Unknown exception type should become a 500
                queryResponseCore = QueryResponseCore.CreateFailure(
                    statusCode: System.Net.HttpStatusCode.InternalServerError,
                    subStatusCodes: null,
                    errorMessage: exception.ToString(),
                    requestCharge: 0,
                    activityId: QueryResponseCore.EmptyGuidString,
                    diagnostics: QueryResponseCore.EmptyDiagnostics);
            }

            return queryResponseCore;
        }

        private static QueryResponseCore CreateFromCosmosException(CosmosException cosmosException)
        {
            QueryResponseCore queryResponseCore = QueryResponseCore.CreateFailure(
                statusCode: cosmosException.StatusCode,
                subStatusCodes: (Microsoft.Azure.Documents.SubStatusCodes)cosmosException.SubStatusCode,
                errorMessage: cosmosException.Message,
                requestCharge: 0,
                activityId: cosmosException.ActivityId,
                diagnostics: QueryResponseCore.EmptyDiagnostics);

            return queryResponseCore;
        }

        private static QueryResponseCore CreateFromDocumentClientException(Microsoft.Azure.Documents.DocumentClientException documentClientException)
        {
            QueryResponseCore queryResponseCore = QueryResponseCore.CreateFailure(
                statusCode: documentClientException.StatusCode.GetValueOrDefault(System.Net.HttpStatusCode.InternalServerError),
                subStatusCodes: null,
                errorMessage: documentClientException.Message,
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
                    $"{nameof(BadRequestException)} due to {nameof(MalformedContinuationTokenException)}",
                    malformedContinuationTokenException);
            }

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException)
            {
                return new InternalServerErrorException(
                    $"{nameof(InternalServerErrorException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    unexpectedQueryPartitionProviderException);
            }

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException)
            {
                return new BadRequestException(
                    $"{nameof(BadRequestException)} due to {nameof(ExpectedQueryPartitionProviderException)}",
                    expectedQueryPartitionProviderException);
            }
        }
    }
}