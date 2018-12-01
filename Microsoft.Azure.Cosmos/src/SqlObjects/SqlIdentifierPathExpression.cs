//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlIdentifierPathExpression : SqlPathExpression
    {
        public SqlIdentifier Value
        {
            get;
            private set;
        }

        public SqlIdentifierPathExpression(SqlPathExpression parentPath, SqlIdentifier value)
            : base(SqlObjectKind.IdentifierPathExpression, parentPath)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            
            this.Value = value;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            if (this.ParentPath != null)
            {
                this.ParentPath.AppendToBuilder(builder);
                builder.Append(".");
            }

            this.Value.AppendToBuilder(builder);
        }
    }
}
