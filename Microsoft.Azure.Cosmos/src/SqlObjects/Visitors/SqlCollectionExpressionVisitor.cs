//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollectionExpressionVisitor
    {
        public abstract void Visit(SqlAliasedCollectionExpression collectionExpression);

        public abstract void Visit(SqlArrayIteratorCollectionExpression collectionExpression);

        public abstract void Visit(SqlJoinCollectionExpression collectionExpression);
    }

    internal abstract class SqlCollectionExpressionVisitor<TResult>
    {
        public abstract TResult Visit(SqlAliasedCollectionExpression collectionExpression);

        public abstract TResult Visit(SqlArrayIteratorCollectionExpression collectionExpression);

        public abstract TResult Visit(SqlJoinCollectionExpression collectionExpression);
    }

    internal abstract class SqlCollectionExpressionVisitor<TInput, TOutput>
    {
        public abstract TOutput Visit(SqlAliasedCollectionExpression collectionExpression, TInput input);

        public abstract TOutput Visit(SqlArrayIteratorCollectionExpression collectionExpression, TInput input);

        public abstract TOutput Visit(SqlJoinCollectionExpression collectionExpression, TInput input);
    }
}
