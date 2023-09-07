// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
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
    sealed class SqlGroupByClause : SqlObject
    {
        private SqlGroupByClause(ImmutableArray<SqlScalarExpression> keySelectorExpressions, SqlScalarExpression valueSelectorExpression)
        {
            foreach (SqlScalarExpression expression in keySelectorExpressions)
            {
                if (expression == null)
                {
                    throw new ArgumentException($"{nameof(keySelectorExpressions)} must not have null items.");
                }
            }

            this.KeySelectorExpressions = keySelectorExpressions;

            this.ValueSelectorExpression = valueSelectorExpression;
        }

        public ImmutableArray<SqlScalarExpression> KeySelectorExpressions { get; }

        // For use during LINQ translation only
        public SqlScalarExpression ValueSelectorExpression { get; }

        public static SqlGroupByClause Create(params SqlScalarExpression[] keySelectorExpressions)
        {
            return new SqlGroupByClause(keySelectorExpressions.ToImmutableArray(), valueSelectorExpression: null);
        }

        public static SqlGroupByClause Create(ImmutableArray<SqlScalarExpression> keySelectorExpressions)
        {
            return new SqlGroupByClause(keySelectorExpressions, valueSelectorExpression: null);
        }

        // For Use with Linq
        public static SqlGroupByClause Create(SqlScalarExpression keySelectorExpression, SqlScalarExpression valueSelectorExpression)
        {
            return new SqlGroupByClause(ImmutableArray.Create(keySelectorExpression), valueSelectorExpression);
        }

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
