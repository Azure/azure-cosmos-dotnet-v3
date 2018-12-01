//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlOrderbyClause : SqlObject
    {
        public SqlOrderbyItem[] OrderbyItems
        {
            get;
            private set;
        }

        public SqlOrderbyClause(SqlOrderbyItem[] orderbyItems)
            : base(SqlObjectKind.OrderByClause)
        {
            if (orderbyItems == null)
            {
                throw new ArgumentNullException("orderbyItems");
            }

            this.OrderbyItems = orderbyItems;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("ORDER BY ");
            this.OrderbyItems[0].AppendToBuilder(builder);
            for(int i = 1; i < this.OrderbyItems.Length; i++)
            {
                builder.Append(", ");
                this.OrderbyItems[i].AppendToBuilder(builder);
            }
        }
    }
}
