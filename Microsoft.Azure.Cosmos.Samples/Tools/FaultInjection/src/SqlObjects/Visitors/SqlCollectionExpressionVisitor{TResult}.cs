// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using Microsoft.Azure.Cosmos.SqlObjects;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlCollectionExpressionVisitor<TResult>
    {
        public abstract TResult Visit(SqlAliasedCollectionExpression collectionExpression);

        public abstract TResult Visit(SqlArrayIteratorCollectionExpression collectionExpression);

        public abstract TResult Visit(SqlJoinCollectionExpression collectionExpression);
    }
}
