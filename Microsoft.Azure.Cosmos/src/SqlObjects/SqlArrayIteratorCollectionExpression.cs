//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlArrayIteratorCollectionExpression : SqlCollectionExpression
    {
        private SqlArrayIteratorCollectionExpression(
           SqlIdentifier alias,
           SqlCollection collection)
            : base(SqlObjectKind.ArrayIteratorCollectionExpression)
        {
            this.Alias = alias ?? throw new ArgumentNullException("alias");
            this.Collection = collection ?? throw new ArgumentNullException("collection");
        }

        public SqlIdentifier Alias
        {
            get;
        }

        public SqlCollection Collection
        {
            get;
        }

        public static SqlArrayIteratorCollectionExpression Create(
            SqlIdentifier alias,
            SqlCollection collection)
        {
            return new SqlArrayIteratorCollectionExpression(alias, collection);
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
