//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    /// <summary>
    /// This class represents a collection expression that is comprised of a collection definition and an optional alias.
    /// </summary>
    /// <example>
    /// FROM Person p
    /// FROM [1, 3, 5, 7] a
    /// </example>
    internal sealed class SqlAliasedCollectionExpression : SqlCollectionExpression
    {
        private SqlAliasedCollectionExpression(
            SqlCollection collection,
            SqlIdentifier alias)
            : base(SqlObjectKind.AliasedCollectionExpression)
        {
            this.Collection = collection ?? throw new ArgumentNullException("collection");
            this.Alias = alias;
        }

        public SqlCollection Collection
        {
            get;
        }

        public SqlIdentifier Alias
        {
            get;
        }

        public static SqlAliasedCollectionExpression Create(
            SqlCollection collection,
            SqlIdentifier alias)
        {
            return new SqlAliasedCollectionExpression(collection, alias);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }

        public override void Accept(SqlCollectionExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
