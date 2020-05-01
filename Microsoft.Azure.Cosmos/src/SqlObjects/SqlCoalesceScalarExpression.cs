//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlCoalesceScalarExpression : SqlScalarExpression
    {
        private SqlCoalesceScalarExpression(
            SqlScalarExpression left,
            SqlScalarExpression right)
            : base(SqlObjectKind.CoalesceScalarExpression)
        {
            if (left == null)
            {
                throw new ArgumentNullException("leftExpression");
            }

            if (right == null)
            {
                throw new ArgumentNullException("rightExpression");
            }

            this.Left = left;
            this.Right = right;
        }

        public SqlScalarExpression Left
        {
            get;
        }

        public SqlScalarExpression Right
        {
            get;
        }

        public static SqlCoalesceScalarExpression Create(
            SqlScalarExpression left,
            SqlScalarExpression right)
        {
            return new SqlCoalesceScalarExpression(left, right);
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
