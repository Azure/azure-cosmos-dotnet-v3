//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Text;

    internal sealed class SqlSelectClause : SqlObject
    {
        public SqlSelectSpec SelectSpec
        {
            get;
            private set;
        }

        public SqlTopSpec TopSpec
        {
            get;
            private set;
        }

        public bool HasDistinct
        {
            get;
            private set;
        }

        public SqlSelectClause(SqlSelectSpec selectSpec, SqlTopSpec topSpec, bool hasDistinct = false)
            : base(SqlObjectKind.SelectClause)
        {
            if (selectSpec == null)
            {
                throw new ArgumentNullException("selectSpec");
            }
            
            this.SelectSpec = selectSpec;
            this.TopSpec = topSpec;
            this.HasDistinct = hasDistinct;
        }

        public override void AppendToBuilder(StringBuilder builder)
        {
            builder.Append("SELECT ");

            if (this.HasDistinct)
            {
                builder.Append("DISTINCT ");
            }

            if(this.TopSpec != null)
            {
                this.TopSpec.AppendToBuilder(builder);
                builder.Append(" ");
            }

            this.SelectSpec.AppendToBuilder(builder);
        }
    }
}
