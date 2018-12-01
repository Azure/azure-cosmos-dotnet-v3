//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Text;

    internal sealed class SqlNullLiteral : SqlLiteral
    {
        public SqlNullLiteral()
            : base(SqlObjectKind.NullLiteral)
        { }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("null");
        }
    }
}
