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
    sealed class SqlOrderByItem : SqlObject
    {
        private SqlOrderByItem(
            SqlScalarExpression expression,
            bool isDescending)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.IsDescending = isDescending;
        }

        public SqlScalarExpression Expression { get; }

        public bool IsDescending { get; }

        public static SqlOrderByItem Create(
            SqlScalarExpression expression,
            bool isDescending)
        {
            return new SqlOrderByItem(expression, isDescending);
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
