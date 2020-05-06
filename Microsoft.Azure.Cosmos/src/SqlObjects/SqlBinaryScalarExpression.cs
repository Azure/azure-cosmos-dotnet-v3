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
        {
            this.LeftExpression = left ?? throw new ArgumentNullException(nameof(left));
            this.OperatorKind = operatorKind;
            this.RightExpression = right ?? throw new ArgumentNullException(nameof(right));
        }

        public SqlScalarExpression LeftExpression { get; }

        public SqlBinaryScalarOperatorKind OperatorKind { get; }

        public SqlScalarExpression RightExpression { get; }

        public static SqlBinaryScalarExpression Create(
            SqlScalarExpression left,
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression right) => new SqlBinaryScalarExpression(left, operatorKind, right);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
