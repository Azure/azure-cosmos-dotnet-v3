//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlArrayScalarExpression : SqlScalarExpression
    {
        public SqlQuery SqlQuery { get; }

        private SqlArrayScalarExpression(SqlQuery sqlQuery)
            : base(SqlObjectKind.ArrayScalarExpression)
        {
            this.SqlQuery = sqlQuery ?? throw new ArgumentNullException(nameof(sqlQuery));
        }

        public static SqlArrayScalarExpression Create(SqlQuery sqlQuery) => new SqlArrayScalarExpression(sqlQuery);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlScalarExpressionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
