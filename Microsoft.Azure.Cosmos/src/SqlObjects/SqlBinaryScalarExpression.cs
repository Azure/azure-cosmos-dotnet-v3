//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlBinaryScalarExpression : SqlScalarExpression
    {
        private SqlBinaryScalarExpression(
            SqlScalarExpression left,
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression right)
            : base(SqlObjectKind.BinaryScalarExpression)
        {
            if (left == null || right == null)
            {
                throw new ArgumentNullException();
            }

            this.OperatorKind = operatorKind;
            this.LeftExpression = left;
            this.RightExpression = right;
        }

        public SqlBinaryScalarOperatorKind OperatorKind
        {
            get;
        }

        public SqlScalarExpression LeftExpression
        {
            get;
        }

        public SqlScalarExpression RightExpression
        {
            get;
        }

        public static SqlBinaryScalarExpression Create(
            SqlScalarExpression left,
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression right)
        {
            return new SqlBinaryScalarExpression(left, operatorKind, right);
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
