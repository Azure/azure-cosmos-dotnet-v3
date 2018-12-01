//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Globalization;
    using System.Text;

    internal class SqlTopSpec : SqlObject
    {
        public int Count
        {
            get;
            private set;
        }

        public SqlTopSpec(int count)
            : base(SqlObjectKind.TopSpec)
        {
            if(count < 0)
            {
                count = 0;
            }

            this.Count = count;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "TOP {0}", this.Count);
        }
    }
}
