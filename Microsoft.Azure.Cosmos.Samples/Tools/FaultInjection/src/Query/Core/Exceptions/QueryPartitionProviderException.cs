// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Exceptions
{
    using System;
    using Microsoft.Azure.Cosmos.Tracing;

    internal abstract class QueryPartitionProviderException : QueryException
    {
        protected QueryPartitionProviderException()
            : base()
        {
        }

        protected QueryPartitionProviderException(string message)
            : base(message)
        {
        }

        protected QueryPartitionProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    internal sealed class UnexpectedQueryPartitionProviderException : QueryPartitionProviderException
    {
        public UnexpectedQueryPartitionProviderException()
            : base()
        {
        }

        public UnexpectedQueryPartitionProviderException(string message)
            : base(message)
        {
        }

        public UnexpectedQueryPartitionProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public override TResult Accept<TResult>(QueryExceptionVisitor<TResult> visitor, ITrace trace)
        {
            return visitor.Visit(this, trace);
        }
    }

    internal sealed class ExpectedQueryPartitionProviderException : QueryPartitionProviderException
    {
        public ExpectedQueryPartitionProviderException()
            : base()
        {
        }

        public ExpectedQueryPartitionProviderException(string message)
            : base(message)
        {
        }

        public ExpectedQueryPartitionProviderException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public override TResult Accept<TResult>(QueryExceptionVisitor<TResult> visitor, ITrace trace)
        {
            return visitor.Visit(this, trace);
        }
    }
}
