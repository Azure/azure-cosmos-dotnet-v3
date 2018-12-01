//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlNumberPathExpression : SqlPathExpression
    {
        public SqlNumberLiteral Value
        {
            get;
            private set;
        }

        public SqlNumberPathExpression(SqlPathExpression parentPath, SqlNumberLiteral value)
            : base(SqlObjectKind.NumberPathExpression, parentPath)
        {
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
