//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
using System;
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlStringPathExpression : SqlPathExpression
    {
        public SqlStringLiteral Value
        {
            get;
            private set;
        }

        public SqlStringPathExpression(SqlPathExpression parentPath, SqlStringLiteral value)
            : base(SqlObjectKind.StringPathExpression, parentPath)
        {
            if(value == null)
            {
                throw new ArgumentNullException("value");
            }

            this.Value = value;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            if (this.ParentPath != null)
            {
                this.ParentPath.AppendToBuilder(builder);
            }

            builder.Append("[");
            this.Value.AppendToBuilder(builder);
            builder.Append("]");
        }
    }
}
