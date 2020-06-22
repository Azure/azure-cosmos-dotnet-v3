//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlCollectionExpressionVisitor
    {
        public abstract void Visit(SqlAliasedCollectionExpression collectionExpression);

        public abstract void Visit(SqlArrayIteratorCollectionExpression collectionExpression);

        public abstract void Visit(SqlJoinCollectionExpression collectionExpression);
    }
}
