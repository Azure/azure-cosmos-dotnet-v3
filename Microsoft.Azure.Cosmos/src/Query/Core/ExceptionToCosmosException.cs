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
        public static bool TryCreateFromException(Exception exception, out CosmosException cosmosException)
        {
            if (exception is CosmosException ce)
            {
                cosmosException = ce;
                return true;
            }

            if (exception is Microsoft.Azure.Documents.DocumentClientException documentClientException)
            {
                cosmosException = CreateFromDocumentClientException(documentClientException);
                return true;
            }

            if (exception is QueryException queryException)
            {
                cosmosException = queryException.Accept(QueryExceptionConverter.Singleton);
                return true;
            }

            if (exception is ChangeFeedException changeFeedException)
            {
                cosmosException = changeFeedException.Accept(ChangeFeedExceptionConverter.Singleton);
                return true;
            }

            if (exception is ExceptionWithStackTraceException exceptionWithStackTrace)
            {
                return TryCreateFromExceptionWithStackTrace(exceptionWithStackTrace, out cosmosException);
            }

            if (exception.InnerException != null)
            {
                // retry with the inner exception
                return ExceptionToCosmosException.TryCreateFromException(exception.InnerException, out cosmosException);
            }

            cosmosException = default;
            return false;
        }

        private static CosmosException CreateFromDocumentClientException(Microsoft.Azure.Documents.DocumentClientException documentClientException)
        {
            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                null);

            return cosmosException;
        }

        private static bool TryCreateFromExceptionWithStackTrace(
            ExceptionWithStackTraceException exceptionWithStackTrace,
            out CosmosException cosmosException)
        {
            // Use the original stack trace from the inner exception.
            if (exceptionWithStackTrace.InnerException is Microsoft.Azure.Documents.DocumentClientException
                || exceptionWithStackTrace.InnerException is CosmosException)
            {
                return ExceptionToCosmosException.TryCreateFromException(exceptionWithStackTrace.InnerException, out cosmosException);
            }

            if (!ExceptionToCosmosException.TryCreateFromException(exceptionWithStackTrace.InnerException, out cosmosException))
            {
                return false;
            }

            cosmosException = CosmosExceptionFactory.Create(
                cosmosException.StatusCode,
                cosmosException.Message,
                exceptionWithStackTrace.StackTrace,
                headers: cosmosException.Headers,
                cosmosException.Trace,
                cosmosException.Error,
                cosmosException.InnerException);
            return true;
        }

        private sealed class QueryExceptionConverter : QueryExceptionVisitor<CosmosException>
        {
            public static readonly QueryExceptionConverter Singleton = new QueryExceptionConverter();

            private QueryExceptionConverter()
            {
            }

            public override CosmosException Visit(MalformedContinuationTokenException malformedContinuationTokenException) => CosmosExceptionFactory.CreateBadRequestException(
                message: malformedContinuationTokenException.Message,
                headers: new Headers(),
                stackTrace: malformedContinuationTokenException.StackTrace,
                innerException: malformedContinuationTokenException);

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException) => CosmosExceptionFactory.CreateInternalServerErrorException(
                message: $"{nameof(CosmosException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                headers: new Headers(),
                innerException: unexpectedQueryPartitionProviderException);

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException) => CosmosExceptionFactory.CreateBadRequestException(
                message: expectedQueryPartitionProviderException.Message,
                headers: new Headers(),
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
                    headers: new Headers(),
                    stackTrace: malformedChangeFeedContinuationTokenException.StackTrace,
                    innerException: malformedChangeFeedContinuationTokenException);
        }
    }
}
