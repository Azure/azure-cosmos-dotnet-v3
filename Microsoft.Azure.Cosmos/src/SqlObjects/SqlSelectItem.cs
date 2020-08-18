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
    sealed class SqlSelectItem : SqlObject
    {
        private SqlSelectItem(
           SqlScalarExpression expression,
           SqlIdentifier alias)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            this.Alias = alias;
        }

        public SqlScalarExpression Expression { get; }

        public SqlIdentifier Alias { get; }

        public static SqlSelectItem Create(
            SqlScalarExpression expression,
            SqlIdentifier alias = null) => new SqlSelectItem(expression, alias);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
