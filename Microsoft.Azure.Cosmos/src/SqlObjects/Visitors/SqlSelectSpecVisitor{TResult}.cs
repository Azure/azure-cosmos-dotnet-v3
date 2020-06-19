// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlSelectSpecVisitor<TResult>
    {
        public abstract TResult Visit(SqlSelectListSpec selectSpec);
        public abstract TResult Visit(SqlSelectStarSpec selectSpec);
        public abstract TResult Visit(SqlSelectValueSpec selectSpec);
    }
}
