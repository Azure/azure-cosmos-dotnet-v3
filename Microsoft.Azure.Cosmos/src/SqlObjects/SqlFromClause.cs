//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlFromClause : SqlObject
    {
        private SqlFromClause(SqlCollectionExpression expression)
        {
            this.Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public SqlCollectionExpression Expression { get; }

        public static SqlFromClause Create(SqlCollectionExpression expression) => new SqlFromClause(expression);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
