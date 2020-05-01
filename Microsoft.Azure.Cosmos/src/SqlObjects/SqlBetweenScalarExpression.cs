//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlBetweenScalarExpression : SqlScalarExpression
    {
        private SqlBetweenScalarExpression(
            SqlScalarExpression expression,
            bool not,
            SqlScalarExpression startInclusive,
            SqlScalarExpression endExclusive)
            : base(SqlObjectKind.BetweenScalarExpression)
        {
            this.Expression = expression;
            this.Not = not;
            this.StartInclusive = startInclusive;
            this.EndInclusive = endExclusive;
        }

        public SqlScalarExpression Expression
        {
            get;
        }

        public bool Not
        {
            get;
        }

        public SqlScalarExpression StartInclusive
        {
            get;
        }

        public SqlScalarExpression EndInclusive
        {
            get;
        }

        public static SqlBetweenScalarExpression Create(
            SqlScalarExpression expression,
            bool not,
            SqlScalarExpression startInclusive,
            SqlScalarExpression endExclusive)
        {
            return new SqlBetweenScalarExpression(expression, not, startInclusive, endExclusive);
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
