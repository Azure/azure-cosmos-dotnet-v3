//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Text;

    internal sealed class SqlIdentifier : SqlObject
    {
        public string Value
        {
            get;
            private set;
        }

        public SqlIdentifier(string value)
            : base(SqlObjectKind.Identifier)
        {
            this.Value = value;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append(this.Value);
        }
    }
}
