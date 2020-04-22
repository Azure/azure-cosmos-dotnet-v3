//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlOrderByItem : SqlObject
    {
        private SqlOrderByItem(
            SqlScalarExpression expression,
            bool isDescending)
            : base(SqlObjectKind.OrderByItem)
        {
            this.Expression = expression ?? throw new ArgumentNullException("expression");
            this.IsDescending = isDescending;
        }

        public SqlScalarExpression Expression
        {
            get;
        }

        public bool IsDescending
        {
            get;
        }

        public static SqlOrderByItem Create(SqlScalarExpression expression, bool isDescending)
        {
            return new SqlOrderByItem(expression, isDescending);
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
