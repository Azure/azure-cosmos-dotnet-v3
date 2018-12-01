//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSelectListSpec : SqlSelectSpec
    {
        public SqlSelectItem[] Items
        {
            get;
            private set;
        }

        public SqlSelectListSpec(SqlSelectItem[] items)
            : base(SqlObjectKind.SelectListSpec)
        {
            if (items == null)
            {
                throw new ArgumentNullException("vecItems");
            }
            
            this.Items = items;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            for (int i = 0; i < this.Items.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                
                this.Items[i].AppendToBuilder(builder);
            }
        }
    }
}
