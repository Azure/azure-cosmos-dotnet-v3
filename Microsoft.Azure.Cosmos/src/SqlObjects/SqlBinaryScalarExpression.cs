//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlBinaryScalarExpression : SqlScalarExpression
    {
        private SqlBinaryScalarExpression(
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression left,
            SqlScalarExpression right)
        {
            this.Left = left ?? throw new ArgumentNullException(nameof(left));
            this.OperatorKind = operatorKind;
            this.Right = right ?? throw new ArgumentNullException(nameof(right));
        }

        public SqlScalarExpression Left { get; }

        public SqlBinaryScalarOperatorKind OperatorKind { get; }

        public SqlScalarExpression Right { get; }

        public static SqlBinaryScalarExpression Create(
            SqlBinaryScalarOperatorKind operatorKind,
            SqlScalarExpression left,
            SqlScalarExpression right) => new SqlBinaryScalarExpression(operatorKind, left, right);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
