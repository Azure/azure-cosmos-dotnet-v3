// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed;
    using Microsoft.Azure.Cosmos.Query.Core.Exceptions;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;

    internal sealed class ExceptionToCosmosException
    {
        private static readonly string EmptyGuidString = Guid.Empty.ToString();

        public static CosmosException CreateFromException(Exception exception)
        {
            if (exception is CosmosException cosmosException)
            {
                return cosmosException;
            }

            if (exception is Microsoft.Azure.Documents.DocumentClientException documentClientException)
            {
                return CreateFromDocumentClientException(documentClientException);
            }

            if (exception is QueryException queryException)
            {
                return queryException.Accept(QueryExceptionConverter.Singleton);
            }

            if (exception is ChangeFeedException changeFeedException)
            {
                return changeFeedException.Accept(ChangeFeedExceptionConverter.Singleton);
            }

            if (exception is ExceptionWithStackTraceException exceptionWithStackTrace)
            {
                return CreateFromExceptionWithStackTrace(exceptionWithStackTrace);
            }

            if (exception.InnerException != null)
            {
                // retry with the inner exception
                return ExceptionToCosmosException.CreateFromException(exception.InnerException);
            }

            return CosmosExceptionFactory.CreateInternalServerErrorException(
                subStatusCode: default,
                message: exception.Message,
                stackTrace: exception.StackTrace,
                activityId: EmptyGuidString,
                requestCharge: 0,
                retryAfter: null,
                headers: null,
                trace: NoOpTrace.Singleton,
                innerException: exception);
        }

        private static CosmosException CreateFromDocumentClientException(Microsoft.Azure.Documents.DocumentClientException documentClientException)
        {
            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                null);

            return cosmosException;
        }

        private static CosmosException CreateFromExceptionWithStackTrace(ExceptionWithStackTraceException exceptionWithStackTrace)
        {
            // Use the original stack trace from the inner exception.
            if (exceptionWithStackTrace.InnerException is Microsoft.Azure.Documents.DocumentClientException
                || exceptionWithStackTrace.InnerException is CosmosException)
            {
                return ExceptionToCosmosException.CreateFromException(exceptionWithStackTrace.InnerException);
            }

            CosmosException cosmosException = ExceptionToCosmosException.CreateFromException(exceptionWithStackTrace.InnerException);
            return CosmosExceptionFactory.Create(
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
                cosmosException.InnerException);
        }

        private sealed class QueryExceptionConverter : QueryExceptionVisitor<CosmosException>
        {
            public static readonly QueryExceptionConverter Singleton = new QueryExceptionConverter();

            private QueryExceptionConverter()
            {
            }

            public override CosmosException Visit(MalformedContinuationTokenException malformedContinuationTokenException) => CosmosExceptionFactory.CreateBadRequestException(
                    message: malformedContinuationTokenException.Message,
                    stackTrace: malformedContinuationTokenException.StackTrace,
                    innerException: malformedContinuationTokenException);

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException) => CosmosExceptionFactory.CreateInternalServerErrorException(
                    message: $"{nameof(CosmosException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    innerException: unexpectedQueryPartitionProviderException);

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException) => CosmosExceptionFactory.CreateBadRequestException(
                    message: expectedQueryPartitionProviderException.Message,
                    stackTrace: expectedQueryPartitionProviderException.StackTrace,
                    innerException: expectedQueryPartitionProviderException);
        }

        private sealed class ChangeFeedExceptionConverter : ChangeFeedExceptionVisitor<CosmosException>
        {
            public static readonly ChangeFeedExceptionConverter Singleton = new ChangeFeedExceptionConverter();

            private ChangeFeedExceptionConverter()
            {
            }

            internal override CosmosException Visit(
                MalformedChangeFeedContinuationTokenException malformedChangeFeedContinuationTokenException) => CosmosExceptionFactory.CreateBadRequestException(
                    message: malformedChangeFeedContinuationTokenException.Message,
                    stackTrace: malformedChangeFeedContinuationTokenException.StackTrace,
                    innerException: malformedChangeFeedContinuationTokenException);
        }
    }
}
