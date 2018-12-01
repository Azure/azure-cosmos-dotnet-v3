//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlUndefinedLiteral : SqlLiteral
    {
        public SqlUndefinedLiteral()
            : base(SqlObjectKind.UndefinedLiteral)
        { }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            builder.Append("undefined");
        }
    }
}
