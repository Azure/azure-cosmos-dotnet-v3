//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlObjectProperty : SqlObject
    {
        public SqlPropertyName Name
        {
            get;
            private set;
        }

        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlObjectProperty(
             SqlPropertyName name,
             SqlScalarExpression expression)
            : base(SqlObjectKind.ObjectProperty)
        {
            if (name == null)
            {
                throw new ArgumentNullException("name");
            }

            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            
            this.Name = name;
            this.Expression = expression;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Name.AppendToBuilder(builder);
            builder.Append(": ");
            this.Expression.AppendToBuilder(builder);
        }
    }
}
