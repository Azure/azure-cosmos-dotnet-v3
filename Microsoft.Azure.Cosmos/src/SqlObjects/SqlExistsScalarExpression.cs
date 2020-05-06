//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlExistsScalarExpression : SqlScalarExpression
    {
        private SqlExistsScalarExpression(SqlQuery subquery)
          : base(SqlObjectKind.ExistsScalarExpression)
        {
            this.Subquery = subquery ?? throw new ArgumentNullException(nameof(subquery));
        }

        public SqlQuery Subquery { get; }

        public static SqlExistsScalarExpression Create(SqlQuery subquery) => new SqlExistsScalarExpression(subquery);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
