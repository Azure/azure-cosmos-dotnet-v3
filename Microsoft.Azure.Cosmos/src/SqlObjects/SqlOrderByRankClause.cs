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
    sealed class SqlOrderByRankClause : SqlObject
    {
        private SqlOrderByRankClause(SqlScalarExpression scoringFunction)
        {
            this.ScoringFunction = scoringFunction ?? throw new ArgumentException($"{nameof(scoringFunction)} must not be null.");
        }

        public SqlScalarExpression ScoringFunction { get; }

        public static SqlOrderByRankClause Create(SqlScalarExpression scoringFunctions)
        {
            return new SqlOrderByRankClause(scoringFunctions);
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
    }
}
