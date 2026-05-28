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
    sealed class SqlOrderByClause : SqlObject
    {
        private SqlOrderByClause(bool rank, ImmutableArray<SqlOrderByItem> orderByItems)
        {
            foreach (SqlOrderByItem sqlOrderbyItem in orderByItems)
            {
                if (sqlOrderbyItem == null)
                {
                    throw new ArgumentException($"{nameof(sqlOrderbyItem)} must not have null items.");
                }
            }

            this.OrderByItems = orderByItems;
            this.Rank = rank;
        }

        public ImmutableArray<SqlOrderByItem> OrderByItems { get; }
        public bool Rank { get; }

        public static SqlOrderByClause Create(params SqlOrderByItem[] orderByItems)
        {
            return new SqlOrderByClause(rank: false, orderByItems.ToImmutableArray());
        }

        public static SqlOrderByClause Create(ImmutableArray<SqlOrderByItem> orderByItems)
        {
            return new SqlOrderByClause(rank: false, orderByItems);
        }

        public static SqlOrderByClause Create(bool rank, params SqlOrderByItem[] orderByItems)
        {
            return new SqlOrderByClause(rank, orderByItems.ToImmutableArray());
        }

        public static SqlOrderByClause Create(bool rank, ImmutableArray<SqlOrderByItem> orderByItems)
        {
            return new SqlOrderByClause(rank, orderByItems);
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
