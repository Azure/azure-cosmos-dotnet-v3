//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlBetweenScalarExpression : SqlScalarExpression
    {
        private SqlBetweenScalarExpression(
            SqlScalarExpression expression,
            SqlScalarExpression startInclusive,
            SqlScalarExpression endInclusive,
            bool not)
        {
            this.Expression = expression;
            this.StartInclusive = startInclusive;
            this.EndInclusive = endInclusive;
            this.Not = not;
        }

        public SqlScalarExpression Expression { get; }

        public bool Not { get; }

        public SqlScalarExpression StartInclusive { get; }

        public SqlScalarExpression EndInclusive { get; }

        public static SqlBetweenScalarExpression Create(
            SqlScalarExpression expression,
            SqlScalarExpression startInclusive,
            SqlScalarExpression endInclusive,
            bool not) => new SqlBetweenScalarExpression(expression, startInclusive, endInclusive, not);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
