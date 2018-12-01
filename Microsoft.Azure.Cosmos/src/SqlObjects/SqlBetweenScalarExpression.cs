//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlBetweenScalarExpression : SqlScalarExpression
    {
        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlScalarExpression LeftExpression
        {
            get;
            private set;
        }

        public SqlScalarExpression RightExpression
        {
            get;
            private set;
        }

        public bool IsNot
        {
            get;
            private set;
        }

        public SqlBetweenScalarExpression(
            SqlScalarExpression expression,
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression,
            bool isNot = false)
            : base(SqlObjectKind.BetweenScalarExpression)
        {
            this.Expression = expression;
            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
            this.IsNot = isNot;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            builder.Append("(")
                   .Append(this.Expression);
            
            if(this.IsNot)
            {
                builder.Append(" NOT");
            }

            builder.Append(" BETWEEN ")
                   .Append(this.LeftExpression)
                   .Append(" AND ")
                   .Append(this.RightExpression)
                   .Append(")");
        }
    }
}
