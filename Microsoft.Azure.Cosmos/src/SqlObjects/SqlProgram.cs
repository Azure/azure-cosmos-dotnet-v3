//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal sealed class SqlProgram : SqlObject
    {
        public SqlQuery Query
        {
            get;
            private set;
        }

        public SqlProgram(SqlQuery query)
            : base(SqlObjectKind.Program)
        {
            this.Query = query;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            this.Query.AppendToBuilder(builder);
        }
    }
}
