//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlConditionalScalarExpression : SqlScalarExpression
    {
        private SqlConditionalScalarExpression(
            SqlScalarExpression condition,
            SqlScalarExpression consequent,
            SqlScalarExpression alternative)
            : base(SqlObjectKind.ConditionalScalarExpression)
        {
            if (condition == null)
            {
                throw new ArgumentNullException("condition");
            }

            if (consequent == null)
            {
                throw new ArgumentNullException("first");
            }

            if (alternative == null)
            {
                throw new ArgumentNullException("second");
            }

            this.Condition = condition;
            this.Consequent = consequent;
            this.Alternative = alternative;
        }

        public SqlScalarExpression Condition
        {
            get;
        }

        public SqlScalarExpression Consequent
        {
            get;
        }

        public SqlScalarExpression Alternative
        {
            get;
        }

        public static SqlConditionalScalarExpression Create(
            SqlScalarExpression condition,
            SqlScalarExpression consequent,
            SqlScalarExpression alternative)
        {
            return new SqlConditionalScalarExpression(condition, consequent, alternative);
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
