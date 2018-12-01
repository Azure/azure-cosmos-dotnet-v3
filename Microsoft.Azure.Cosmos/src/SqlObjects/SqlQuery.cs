//-----------------------------------------------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal class SqlQuery : SqlObject
    {
        public SqlQuery(
            SqlSelectClause selectClause,
            SqlFromClause fromClause,
            SqlWhereClause whereClause,
            SqlOrderbyClause orderbyClause)
            : base(SqlObjectKind.Query)
        {
            this.SelectClause = selectClause;
            this.FromClause = fromClause;
            this.WhereClause = whereClause;
            this.OrderbyClause = orderbyClause;
        }

        public SqlSelectClause SelectClause
        {
            get;
            set;
        }

        public SqlFromClause FromClause
        {
            get;
            set;
        }

        public SqlWhereClause WhereClause
        {
            get;
            set;
        }

        public SqlOrderbyClause OrderbyClause
        {
            get;
            set;
        }

        public override void AppendToBuilder(System.Text.StringBuilder builder)
        {
            if (this.SelectClause != null)
            {
                this.SelectClause.AppendToBuilder(builder);
                builder.Append(" ");
            }

            if (this.FromClause != null)
            {
                this.FromClause.AppendToBuilder(builder);
                builder.Append(" ");
            }

            if (this.WhereClause != null)
            {
                this.WhereClause.AppendToBuilder(builder);
                builder.Append(" ");
            }

            if (this.OrderbyClause != null)
            {
                this.OrderbyClause.AppendToBuilder(builder);
                builder.Append(" ");
            }
        }
    }
}
