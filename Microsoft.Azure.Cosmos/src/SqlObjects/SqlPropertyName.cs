//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System.Text.RegularExpressions;

    internal sealed class SqlPropertyName : SqlObject
    {
        public string Value
        {
            get;
            private set;
        }

        public SqlPropertyName(string value)
            : base(SqlObjectKind.PropertyName)
        {
            this.Value = value;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            builder.Append('"').Append(this.Value).Append('"');
        }
    }
}
