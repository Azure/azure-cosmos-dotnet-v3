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
            SqlScalarExpression first,
            SqlScalarExpression second)
            : base(SqlObjectKind.ConditionalScalarExpression)
        {
            this.ConditionExpression = condition ?? throw new ArgumentNullException("condition");
            this.FirstExpression = first ?? throw new ArgumentNullException("first");
            this.SecondExpression = second ?? throw new ArgumentNullException("second");
        }

        public SqlScalarExpression ConditionExpression
        {
            get;
        }

        public SqlScalarExpression FirstExpression
        {
            get;
        }

        public SqlScalarExpression SecondExpression
        {
            get;
        }

        public static SqlConditionalScalarExpression Create(
            SqlScalarExpression condition,
            SqlScalarExpression first,
            SqlScalarExpression second)
        {
            return new SqlConditionalScalarExpression(condition, first, second);
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
