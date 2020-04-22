//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlFromClause : SqlObject
    {
        private SqlFromClause(SqlCollectionExpression expression)
            : base(SqlObjectKind.FromClause)
        {
            this.Expression = expression ?? throw new ArgumentNullException("expression");
        }

        public SqlCollectionExpression Expression
        {
            get;
        }

        public static SqlFromClause Create(SqlCollectionExpression expression)
        {
            return new SqlFromClause(expression);
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
