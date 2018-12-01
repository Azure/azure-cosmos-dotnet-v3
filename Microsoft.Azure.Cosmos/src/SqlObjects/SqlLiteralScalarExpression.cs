//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlLiteralScalarExpression : SqlScalarExpression
    {
        public SqlLiteral Literal
        {
            get;
            private set;
        }

        public SqlLiteralScalarExpression(SqlLiteral literal)
            : base(SqlObjectKind.LiteralScalarExpression)
        {
            if (literal == null)
            {
                throw new ArgumentNullException("literal");
            }
            
            this.Literal = literal;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            this.Literal.AppendToBuilder(builder);
        }
    }
}
