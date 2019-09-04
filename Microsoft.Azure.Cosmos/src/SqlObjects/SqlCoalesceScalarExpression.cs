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
            if (leftExpression == null)
            {
                throw new ArgumentNullException("leftExpression");
            }

            if (rightExpression == null)
            {
                throw new ArgumentNullException("rightExpression");
            }

            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
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
