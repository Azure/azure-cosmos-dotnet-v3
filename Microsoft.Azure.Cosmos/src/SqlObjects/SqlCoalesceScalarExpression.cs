//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlCoalesceScalarExpression : SqlScalarExpression
    {
        private SqlCoalesceScalarExpression(
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression)
            : base(SqlObjectKind.CoalesceScalarExpression)
        {
            this.LeftExpression = leftExpression ?? throw new ArgumentNullException("leftExpression");
            this.RightExpression = rightExpression ?? throw new ArgumentNullException("rightExpression");
        }

        public SqlScalarExpression LeftExpression
        {
            get;
        }

        public SqlScalarExpression RightExpression
        {
            get;
        }

        public static SqlCoalesceScalarExpression Create(
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression)
        {
            return new SqlCoalesceScalarExpression(leftExpression, rightExpression);
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

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
