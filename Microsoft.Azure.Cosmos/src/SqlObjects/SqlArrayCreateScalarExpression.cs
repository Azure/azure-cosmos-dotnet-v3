//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Immutable;
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
        private static readonly SqlArrayCreateScalarExpression Empty = new SqlArrayCreateScalarExpression(ImmutableArray<SqlScalarExpression>.Empty);

        private SqlArrayCreateScalarExpression(ImmutableArray<SqlScalarExpression> items)
        {
            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(item)} must not have null items.");
                }
            }

            this.Items = items;
        }

        public ImmutableArray<SqlScalarExpression> Items { get; }

        public static SqlArrayCreateScalarExpression Create()
        {
            return SqlArrayCreateScalarExpression.Empty;
        }

        public static SqlArrayCreateScalarExpression Create(params SqlScalarExpression[] items)
        {
            return new SqlArrayCreateScalarExpression(items.ToImmutableArray());
        }

        public static SqlArrayCreateScalarExpression Create(ImmutableArray<SqlScalarExpression> items)
        {
            return new SqlArrayCreateScalarExpression(items);
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
