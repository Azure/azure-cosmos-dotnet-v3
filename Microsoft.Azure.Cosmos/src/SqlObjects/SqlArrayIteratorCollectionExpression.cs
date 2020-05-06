//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlArrayIteratorCollectionExpression : SqlCollectionExpression
    {
        private SqlArrayIteratorCollectionExpression(
           SqlIdentifier identifier,
           SqlCollection collection)
        {
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
            this.Collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public SqlIdentifier Identifier { get; }

        public SqlCollection Collection { get; }

        public static SqlArrayIteratorCollectionExpression Create(
            SqlIdentifier identifier,
            SqlCollection collection) => new SqlArrayIteratorCollectionExpression(identifier, collection);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlCollectionExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
