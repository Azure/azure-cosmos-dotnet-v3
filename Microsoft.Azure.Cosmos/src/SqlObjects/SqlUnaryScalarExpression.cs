//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlUnaryScalarExpression : SqlScalarExpression
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
            SqlScalarExpression expression) => new SqlUnaryScalarExpression(operatorKind, expression);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
