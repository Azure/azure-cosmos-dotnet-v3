//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

    // This class represents a collection expression that is comprised of a collection definition and an 
    // optional alias.
    // Examples:
    //  FROM Person p
    //  FROM [1, 3, 5, 7] a

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlAliasedCollectionExpression : SqlCollectionExpression
    {
        private SqlAliasedCollectionExpression(
            SqlCollection collection,
            SqlIdentifier alias)
        {
            this.Collection = collection ?? throw new ArgumentNullException(nameof(collection));
            this.Alias = alias;
        }

        public SqlCollection Collection { get; }

        public SqlIdentifier Alias { get; }

        public static SqlAliasedCollectionExpression Create(
            SqlCollection collection,
            SqlIdentifier alias) => new SqlAliasedCollectionExpression(collection, alias);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlCollectionExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
