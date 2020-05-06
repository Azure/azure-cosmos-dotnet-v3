//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlOrderbyClause : SqlObject
    {
        private SqlOrderbyClause(IReadOnlyList<SqlOrderByItem> orderbyItems)
        {
            if (orderbyItems == null)
            {
                throw new ArgumentNullException("orderbyItems");
            }

            foreach (SqlOrderByItem sqlOrderbyItem in orderbyItems)
            {
                if (sqlOrderbyItem == null)
                {
                    throw new ArgumentException($"{nameof(sqlOrderbyItem)} must have have null items.");
                }
            }

            this.OrderbyItems = orderbyItems;
        }

        public IReadOnlyList<SqlOrderByItem> OrderbyItems { get; }

        public static SqlOrderbyClause Create(params SqlOrderByItem[] orderbyItems) => new SqlOrderbyClause(orderbyItems);

        public static SqlOrderbyClause Create(IReadOnlyList<SqlOrderByItem> orderbyItems) => new SqlOrderbyClause(orderbyItems);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
