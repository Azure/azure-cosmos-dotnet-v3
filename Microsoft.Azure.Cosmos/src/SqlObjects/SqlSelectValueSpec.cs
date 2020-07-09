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
    sealed class SqlSelectValueSpec : SqlSelectSpec
    {
        private SqlSelectValueSpec(
            SqlScalarExpression expression)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public SqlScalarExpression Expression { get; }

        public static SqlSelectValueSpec Create(SqlScalarExpression expression) => new SqlSelectValueSpec(expression);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlSelectSpecVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
