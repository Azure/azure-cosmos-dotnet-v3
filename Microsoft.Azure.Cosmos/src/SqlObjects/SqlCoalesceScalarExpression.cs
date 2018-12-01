//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.Text;

    internal sealed class SqlCoalesceScalarExpression : SqlScalarExpression
    {
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

        public SqlCoalesceScalarExpression(
            SqlScalarExpression leftExpression,
            SqlScalarExpression rightExpression)
            : base(SqlObjectKind.CoalesceScalarExpression)
        {
            if (leftExpression == null)
            {
                throw new ArgumentNullException("leftExpression");
            }

            if (rightExpression == null)
            {
                throw new ArgumentNullException("rightExpression");
            }

            this.LeftExpression = leftExpression;
            this.RightExpression = rightExpression;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("(");
            this.LeftExpression.AppendToBuilder(builder);
            builder.Append(" ?? ");
            this.RightExpression.AppendToBuilder(builder);
            builder.Append(")");
        }
    }
}