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
            this.SqlQuery = sqlQuery ?? throw new ArgumentNullException($"{nameof(sqlQuery)} can not be null");
        }

        public static SqlArrayScalarExpression Create(SqlQuery sqlQuery)
        {
            return new SqlArrayScalarExpression(sqlQuery);
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

        public override void Accept(SqlScalarExpressionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
