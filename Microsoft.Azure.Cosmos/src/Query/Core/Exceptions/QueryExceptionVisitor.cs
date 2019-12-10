// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Exceptions
{
    internal abstract class QueryExceptionVisitor<TResult>
    {
        public abstract TResult Visit(MalformedContinuationTokenException malformedContinuationTokenException);
        public abstract TResult Visit(UnexpectedQueryPartitionProviderException unexpectedQueryPartitionProviderException);
        public abstract TResult Visit(ExpectedQueryPartitionProviderException expectedQueryPartitionProviderException);
    }
}