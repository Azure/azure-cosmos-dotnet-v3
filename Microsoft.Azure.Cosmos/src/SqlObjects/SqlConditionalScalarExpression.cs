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
            SqlScalarExpression alternative) => new SqlConditionalScalarExpression(condition, consequent, alternative);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
