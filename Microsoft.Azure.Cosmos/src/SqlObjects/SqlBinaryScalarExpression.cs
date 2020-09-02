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
    sealed class SqlBinaryScalarExpression : SqlScalarExpression
    {
        private SqlBinaryScalarExpression(
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression left,
            SqlScalarExpression right)
        {
            this.LeftExpression = left ?? throw new ArgumentNullException(nameof(left));
            this.OperatorKind = operatorKind;
            this.RightExpression = right ?? throw new ArgumentNullException(nameof(right));
        }

        public SqlScalarExpression LeftExpression { get; }

        public SqlBinaryScalarOperatorKind OperatorKind { get; }

        public SqlScalarExpression RightExpression { get; }

        public static SqlBinaryScalarExpression Create(
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression left,
            SqlScalarExpression right)
        {
            return new SqlBinaryScalarExpression(operatorKind, left, right);
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
