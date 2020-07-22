//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlArrayCreateScalarExpression : SqlScalarExpression
    {
        private static readonly SqlArrayCreateScalarExpression Empty = new SqlArrayCreateScalarExpression(new List<SqlScalarExpression>());

        private SqlArrayCreateScalarExpression(IReadOnlyList<SqlScalarExpression> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException($"{nameof(items)} must not be null.");
            }

            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(item)} must not have null items.");
                }
            }

            this.Items = new List<SqlScalarExpression>(items);
        }

        public IReadOnlyList<SqlScalarExpression> Items { get; }

        public static SqlArrayCreateScalarExpression Create() => SqlArrayCreateScalarExpression.Empty;

        public static SqlArrayCreateScalarExpression Create(params SqlScalarExpression[] items) => new SqlArrayCreateScalarExpression(items);

        public static SqlArrayCreateScalarExpression Create(IReadOnlyList<SqlScalarExpression> items) => new SqlArrayCreateScalarExpression(items);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
