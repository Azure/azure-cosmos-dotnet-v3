//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlSubqueryScalarExpression : SqlScalarExpression
    {
        private SqlSubqueryScalarExpression(SqlQuery query)
            : base(SqlObjectKind.SubqueryScalarExpression)
        {
            this.Query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public SqlQuery Query { get; }

        public static SqlSubqueryScalarExpression Create(SqlQuery query) => new SqlSubqueryScalarExpression(query);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
