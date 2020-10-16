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
    sealed class SqlSelectListSpec : SqlSelectSpec
    {
        private SqlSelectListSpec(ImmutableArray<SqlSelectItem> items)
        {
            foreach (SqlSelectItem item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(items)} must not contain null items.");
                }
            }

            this.Items = items;
        }

        public ImmutableArray<SqlSelectItem> Items { get; }

        public static SqlSelectListSpec Create(params SqlSelectItem[] items)
        {
            return new SqlSelectListSpec(items.ToImmutableArray());
        }

        public static SqlSelectListSpec Create(ImmutableArray<SqlSelectItem> items)
        {
            return new SqlSelectListSpec(items);
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

        public override void Accept(SqlSelectSpecVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
