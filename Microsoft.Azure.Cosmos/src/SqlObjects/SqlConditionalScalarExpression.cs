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
    sealed class SqlConditionalScalarExpression : SqlScalarExpression
    {
        private SqlConditionalScalarExpression(
            SqlScalarExpression condition,
            SqlScalarExpression consequent,
            SqlScalarExpression alternative)
        {
            this.Condition = condition ?? throw new ArgumentNullException(nameof(condition));
            this.Consequent = consequent ?? throw new ArgumentNullException(nameof(consequent));
            this.Alternative = alternative ?? throw new ArgumentNullException(nameof(alternative));
        }

        public SqlScalarExpression Condition { get; }

        public SqlScalarExpression Consequent { get; }

        public SqlScalarExpression Alternative { get; }

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
