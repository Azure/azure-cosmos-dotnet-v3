//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlArrayIteratorCollectionExpression : SqlCollectionExpression
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
            SqlCollection collection)
        {
            return new SqlArrayIteratorCollectionExpression(identifier, collection);
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
