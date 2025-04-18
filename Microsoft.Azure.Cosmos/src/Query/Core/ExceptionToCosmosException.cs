﻿// ------------------------------------------------------------
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
        public static bool TryCreateFromException(
            Exception exception, 
            ITrace trace,
            out CosmosException cosmosException)
        {
            if (exception is CosmosException ce)
            {
                cosmosException = ce;
                return true;
            }

            if (exception is Microsoft.Azure.Documents.DocumentClientException documentClientException)
            {
                cosmosException = CreateFromDocumentClientException(documentClientException, trace);
                return true;
            }

            if (exception is QueryException queryException)
            {
                cosmosException = queryException.Accept(QueryExceptionConverter.Singleton, trace);
                return true;
            }

            if (exception is ChangeFeedException changeFeedException)
            {
                cosmosException = changeFeedException.Accept(ChangeFeedExceptionConverter.Singleton, trace);
                return true;
            }

            if (exception is ExceptionWithStackTraceException exceptionWithStackTrace)
            {
                return TryCreateFromExceptionWithStackTrace(exceptionWithStackTrace, trace, out cosmosException);
            }

            if (exception.InnerException != null)
            {
                // retry with the inner exception
                return ExceptionToCosmosException.TryCreateFromException(
                    exception.InnerException,
                    trace,
                    out cosmosException);
            }

            cosmosException = default;
            return false;
        }

        private static CosmosException CreateFromDocumentClientException(
            Microsoft.Azure.Documents.DocumentClientException documentClientException,
            ITrace trace)
        {
            CosmosException cosmosException = CosmosExceptionFactory.Create(
                documentClientException,
                trace);

            return cosmosException;
        }

        private static bool TryCreateFromExceptionWithStackTrace(
            ExceptionWithStackTraceException exceptionWithStackTrace,
            ITrace trace,
            out CosmosException cosmosException)
        {
            Exception innerException = ExceptionWithStackTraceException.UnWrapMonadExcepion(exceptionWithStackTrace, trace);

            if (!ExceptionToCosmosException.TryCreateFromException(
                innerException,
                trace,
                out cosmosException))
            {
                return false;
            }

            if (innerException is not CosmosException && innerException is not Documents.DocumentClientException)
            {
#pragma warning disable CDX1002 // DontUseExceptionStackTrace
                cosmosException = CosmosExceptionFactory.Create(
                    cosmosException.StatusCode,
                    cosmosException.Message,
                    exceptionWithStackTrace.StackTrace,
                    headers: cosmosException.Headers,
                    cosmosException.Trace,
                    cosmosException.Error,
                    cosmosException.InnerException);
#pragma warning restore CDX1002 // DontUseExceptionStackTrace
            }

            return true;
        }

        private sealed class QueryExceptionConverter : QueryExceptionVisitor<CosmosException>
        {
            public static readonly QueryExceptionConverter Singleton = new QueryExceptionConverter();

            private QueryExceptionConverter()
            {
            }

            public override CosmosException Visit(MalformedContinuationTokenException malformedContinuationTokenException, ITrace trace)
            {
                Headers headers = new Headers()
                {
                    SubStatusCode = Documents.SubStatusCodes.MalformedContinuationToken
                };
#pragma warning disable CDX1002 // DontUseExceptionStackTrace
                return CosmosExceptionFactory.CreateBadRequestException(
                    message: malformedContinuationTokenException.Message,
                    headers: headers,
                    stackTrace: malformedContinuationTokenException.StackTrace,
                    innerException: malformedContinuationTokenException,
                    trace: trace);
#pragma warning restore CDX1002 // DontUseExceptionStackTrace
            }

            public override CosmosException Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException, ITrace trace)
            {
                return CosmosExceptionFactory.CreateInternalServerErrorException(
                    message: $"{nameof(CosmosException)} due to {nameof(UnexpectedQueryPartitionProviderException)}",
                    headers: new Headers(),
                    innerException: unexpectedQueryPartitionProviderException,
                    trace: trace);
            }

            public override CosmosException Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException, ITrace trace)
            {
#pragma warning disable CDX1002 // DontUseExceptionStackTrace
                return CosmosExceptionFactory.CreateBadRequestException(
                    message: expectedQueryPartitionProviderException.Message,
                    headers: new Headers(),
                    stackTrace: expectedQueryPartitionProviderException.StackTrace,
                    innerException: expectedQueryPartitionProviderException,
                    trace: trace);
#pragma warning restore CDX1002 // DontUseExceptionStackTrace
            }
        }

        private sealed class ChangeFeedExceptionConverter : ChangeFeedExceptionVisitor<CosmosException>
        {
            public static readonly ChangeFeedExceptionConverter Singleton = new ChangeFeedExceptionConverter();

            private ChangeFeedExceptionConverter()
            {
            }

            internal override CosmosException Visit(
                MalformedChangeFeedContinuationTokenException malformedChangeFeedContinuationTokenException,
                ITrace trace)
            {
#pragma warning disable CDX1002 // DontUseExceptionStackTrace
                return CosmosExceptionFactory.CreateBadRequestException(
                    message: malformedChangeFeedContinuationTokenException.Message,
                    headers: new Headers()
                    {
                        SubStatusCode = Documents.SubStatusCodes.MalformedContinuationToken
                    },
                    stackTrace: malformedChangeFeedContinuationTokenException.StackTrace,
                    innerException: malformedChangeFeedContinuationTokenException,
                    trace: trace);
#pragma warning restore CDX1002 // DontUseExceptionStackTrace
            }
        }
    }
}
