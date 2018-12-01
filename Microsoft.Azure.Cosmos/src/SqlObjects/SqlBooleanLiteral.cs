//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlBooleanLiteral : SqlLiteral
    {
        public bool Value
        {
            get;
            private set;
        }

        public SqlBooleanLiteral(bool value)
            : base(SqlObjectKind.BooleanLiteral)
        { 
            this.Value = value;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            builder.Append(this.Value ? "true" : "false");
        }
    }
}