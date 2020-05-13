// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlParameterRefScalarExpression : SqlScalarExpression
    {
        private SqlParameterRefScalarExpression(SqlParameter sqlParameter)
        {
            this.Parameter = sqlParameter ?? throw new ArgumentNullException(nameof(sqlParameter));
        }

        public SqlParameter Parameter { get; }

        public static SqlParameterRefScalarExpression Create(SqlParameter sqlParameter) => new SqlParameterRefScalarExpression(sqlParameter);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
