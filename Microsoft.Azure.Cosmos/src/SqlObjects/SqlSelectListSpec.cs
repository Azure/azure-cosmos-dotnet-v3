//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlSelectListSpec : SqlSelectSpec
    {
        private SqlSelectListSpec(IReadOnlyList<SqlSelectItem> items)
            : base(SqlObjectKind.SelectListSpec)
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
