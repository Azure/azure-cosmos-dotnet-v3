//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Text;

    internal sealed class SqlSelectStarSpec : SqlSelectSpec
    {
        public SqlSelectStarSpec()
            : base(SqlObjectKind.SelectStarSpec)
        { }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("*");
        }
    }
}
