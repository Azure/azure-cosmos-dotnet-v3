//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlWhereClause : SqlObject
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
