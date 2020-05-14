// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlGroupByClause : SqlObject
    {
        private SqlGroupByClause(IReadOnlyList<SqlScalarExpression> expressions)
        {
            if (expressions == null)
            {
                throw new ArgumentNullException($"{nameof(expressions)}");
            }

            foreach (SqlScalarExpression expression in expressions)
            {
                if (expression == null)
                {
                    throw new ArgumentException($"{nameof(expressions)} must not have null items.");
                }
            }

            this.Expressions = expressions;
        }

        public IReadOnlyList<SqlScalarExpression> Expressions { get; }

        public static SqlGroupByClause Create(params SqlScalarExpression[] expressions) => new SqlGroupByClause(expressions);

        public static SqlGroupByClause Create(IReadOnlyList<SqlScalarExpression> expressions) => new SqlGroupByClause(expressions);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
