//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSelectItem : SqlObject
    {
        public SqlScalarExpression Expression
        {
            get;
            private set;
        }

        public SqlIdentifier Alias
        {
            get;
            private set;
        }

        public SqlSelectItem(
           SqlScalarExpression expression,
           SqlIdentifier alias)
            : base(SqlObjectKind.SelectItem)
        {
            if (expression == null)
            {
                throw new ArgumentNullException("expression");
            }
            
            this.Expression = expression;
            this.Alias = alias;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            this.Expression.AppendToBuilder(builder);
            if (this.Alias != null)
            {
                builder.Append(" AS ");
                this.Alias.AppendToBuilder(builder);
            }
        }
    }
}
