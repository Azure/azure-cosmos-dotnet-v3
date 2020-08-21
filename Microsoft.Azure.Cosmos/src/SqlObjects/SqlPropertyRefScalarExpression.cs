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
    sealed class SqlPropertyRefScalarExpression : SqlScalarExpression
    {
        private SqlPropertyRefScalarExpression(
            SqlScalarExpression member,
            SqlIdentifier identifier)
        {
            this.Member = member;
            this.Identifier = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        public SqlIdentifier Identifier { get; }

        public SqlScalarExpression Member { get; }

        public static SqlPropertyRefScalarExpression Create(
            SqlScalarExpression member,
            SqlIdentifier identifier) => new SqlPropertyRefScalarExpression(member, identifier);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
