//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSelectValueSpec : SqlSelectSpec
    {
        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlSelectValueSpec(
            SqlScalarExpression expression)
            : base(SqlObjectKind.SelectValueSpec)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            
            this.Expression = expression;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("VALUE ");
            this.Expression.AppendToBuilder(builder);
        }
    }
}
