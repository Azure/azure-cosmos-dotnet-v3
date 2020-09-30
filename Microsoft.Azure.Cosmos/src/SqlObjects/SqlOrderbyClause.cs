//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using System;
    using System.Collections.Immutable;
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    sealed class SqlOrderbyClause : SqlObject
    {
        private SqlOrderbyClause(ImmutableArray<SqlOrderByItem> orderbyItems)
        {
            foreach (SqlOrderByItem sqlOrderbyItem in orderbyItems)
            {
                if (sqlOrderbyItem == null)
                {
                    throw new ArgumentException($"{nameof(sqlOrderbyItem)} must have have null items.");
                }
            }

            this.OrderbyItems = orderbyItems;
        }

        public ImmutableArray<SqlOrderByItem> OrderbyItems { get; }

        public static SqlOrderbyClause Create(params SqlOrderByItem[] orderbyItems)
        {
            return new SqlOrderbyClause(orderbyItems.ToImmutableArray());
        }

        public static SqlOrderbyClause Create(ImmutableArray<SqlOrderByItem> orderbyItems)
        {
            return new SqlOrderbyClause(orderbyItems);
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
