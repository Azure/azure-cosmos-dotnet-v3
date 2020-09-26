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
    sealed class SqlUnaryScalarExpression : SqlScalarExpression
    {
        private SqlUnaryScalarExpression(
            SqlUnaryScalarOperatorKind operatorKind,
            SqlScalarExpression expression)
        {
            this.OperatorKind = operatorKind;
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public SqlUnaryScalarOperatorKind OperatorKind { get; }

        public SqlScalarExpression Expression { get; }

        public static SqlUnaryScalarExpression Create(
            SqlUnaryScalarOperatorKind operatorKind,
            SqlScalarExpression expression)
        {
            return new SqlUnaryScalarExpression(operatorKind, expression);
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
