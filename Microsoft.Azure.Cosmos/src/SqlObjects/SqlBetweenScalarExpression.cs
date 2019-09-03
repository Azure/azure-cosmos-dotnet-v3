//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlBetweenScalarExpression : SqlScalarExpression
    {
        private SqlBetweenScalarExpression(
            SqlScalarExpression expression,
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression,
            bool isNot = false)
            : base(SqlObjectKind.BetweenScalarExpression)
        {
            this.Expression = expression;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
            this.IsNot = isNot;
        }

        public SqlScalarExpression Expression
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

        public bool IsNot
        {
            get;
        }

        public static SqlBetweenScalarExpression Create(
            SqlScalarExpression expression,
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression,
            bool isNot = false)
        {
            return new SqlBetweenScalarExpression(expression, leftExpression, rightExpression, isNot);
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
