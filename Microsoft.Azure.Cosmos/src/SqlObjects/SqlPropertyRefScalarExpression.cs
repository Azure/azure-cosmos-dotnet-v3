//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlPropertyRefScalarExpression : SqlScalarExpression
    {
        private SqlPropertyRefScalarExpression(
            SqlScalarExpression member,
            SqlIdentifier identifier)
            : base(SqlObjectKind.PropertyRefScalarExpression)
        {
            this.Member = member;
            this.Identifer = identifier ?? throw new ArgumentNullException(nameof(identifier));
        }

        public SqlIdentifier Identifer { get; }

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
