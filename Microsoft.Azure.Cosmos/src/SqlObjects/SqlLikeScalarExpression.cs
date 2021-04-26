//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif

    sealed class SqlLikeScalarExpression : SqlScalarExpression
    {
        private SqlLikeScalarExpression(
            SqlScalarExpression expression,
            SqlScalarExpression pattern,
            bool not,
            SqlStringLiteral escapeSequence = null)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            this.Not = not;
            this.EscapeSequence = escapeSequence;
        }

        public SqlScalarExpression Expression { get; }

        public SqlScalarExpression Pattern { get; }

        public bool Not { get; }

        public SqlStringLiteral EscapeSequence { get; }

        public static SqlLikeScalarExpression Create(
            SqlScalarExpression expression,
            SqlScalarExpression pattern,
            bool not,
            SqlStringLiteral escapeSequence = null)
        {
            return new SqlLikeScalarExpression(expression, pattern, not, escapeSequence);
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
