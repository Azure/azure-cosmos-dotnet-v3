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
    sealed class SqlWhereClause : SqlObject
    {
        private SqlWhereClause(SqlScalarExpression filterExpression)
        {
            this.FilterExpression = filterExpression ?? throw new ArgumentNullException(nameof(filterExpression));
        }

        public SqlScalarExpression FilterExpression { get; }

        public static SqlWhereClause Create(SqlScalarExpression filterExpression) => new SqlWhereClause(filterExpression);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
