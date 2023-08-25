// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query.Core.Exceptions
{
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class QueryExceptionVisitor<TResult>
    {
        public abstract TResult Visit(MalformedContinuationTokenException malformedContinuationTokenException, ITrace trace);
        public abstract TResult Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException, ITrace trace);
        public abstract TResult Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException, ITrace trace);
    }
}