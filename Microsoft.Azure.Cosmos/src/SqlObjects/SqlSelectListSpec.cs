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
    sealed class SqlSelectListSpec : SqlSelectSpec
    {
        private SqlSelectListSpec(IReadOnlyList<SqlSelectItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException($"{nameof(items)} must not be null.");
            }

            foreach (SqlSelectItem item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(items)} must not contain null items.");
                }
            }

            this.Items = new List<SqlSelectItem>(items);
        }

        public IReadOnlyList<SqlSelectItem> Items { get; }

        public static SqlSelectListSpec Create(params SqlSelectItem[] items) => new SqlSelectListSpec(items);

        public static SqlSelectListSpec Create(IReadOnlyList<SqlSelectItem> items) => new SqlSelectListSpec(items);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlSelectSpecVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
