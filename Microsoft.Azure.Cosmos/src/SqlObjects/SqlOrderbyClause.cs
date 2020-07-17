//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlOrderByClause : SqlObject
    {
        private SqlOrderByClause(IReadOnlyList<SqlOrderByItem> orderbyItems)
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

        public static SqlOrderByClause Create(params SqlOrderByItem[] orderbyItems) => new SqlOrderByClause(orderbyItems);

        public static SqlOrderByClause Create(IReadOnlyList<SqlOrderByItem> orderbyItems) => new SqlOrderByClause(orderbyItems);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
