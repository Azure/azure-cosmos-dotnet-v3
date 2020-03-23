//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlQuery : SqlObject
    {
        private SqlQuery(
            SqlSelectClause selectClause,
            SqlFromClause fromClause,
            SqlWhereClause whereClause,
            SqlGroupByClause groupByClause,
            SqlOrderbyClause orderbyClause,
            SqlOffsetLimitClause offsetLimitClause)
            : base(SqlObjectKind.Query)
        {
            if (selectClause == null)
            {
                throw new ArgumentNullException($"{nameof(selectClause)} must not be null.");
            }

            this.SelectClause = selectClause;
            this.FromClause = fromClause;
            this.WhereClause = whereClause;
            this.GroupByClause = groupByClause;
            this.OrderbyClause = orderbyClause;
            this.OffsetLimitClause = offsetLimitClause;
        }

        public SqlSelectClause SelectClause
        {
            get;
        }

        public SqlFromClause FromClause
        {
            get;
        }

        public SqlWhereClause WhereClause
        {
            get;
        }

        public SqlGroupByClause GroupByClause
        {
            get;
        }

        public SqlOrderbyClause OrderbyClause
        {
            get;
        }

        public SqlOffsetLimitClause OffsetLimitClause
        {
            get;
        }

        public static SqlQuery Create(
            SqlSelectClause selectClause,
            SqlFromClause fromClause,
            SqlWhereClause whereClause,
            SqlGroupByClause groupByClause,
            SqlOrderbyClause orderByClause,
            SqlOffsetLimitClause offsetLimitClause)
        {
            return new SqlQuery(selectClause, fromClause, whereClause, groupByClause, orderByClause, offsetLimitClause);
        }

        public override void Accept(SqlObjectVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
