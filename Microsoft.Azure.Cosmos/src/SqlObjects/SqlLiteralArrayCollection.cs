//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;
    using System.Collections.Generic;

    internal sealed class SqlLiteralArrayCollection : SqlCollection
    {
        private static readonly SqlLiteralArrayCollection Empty = new SqlLiteralArrayCollection(new List<SqlScalarExpression>());
        private SqlLiteralArrayCollection(IReadOnlyList<SqlScalarExpression> items)
            : base(SqlObjectKind.LiteralArrayCollection)
        {
            if (items == null)
            {
                throw new ArgumentNullException($"{nameof(items)} must not be null.");
            }

            foreach (SqlScalarExpression item in items)
            {
                if (item == null)
                {
                    throw new ArgumentException($"{nameof(item)} must not have null items.");
                }
            }

            this.Items = new List<SqlScalarExpression>(items);
        }

        public IReadOnlyList<SqlScalarExpression> Items
        {
            get;
        }

        public static SqlLiteralArrayCollection Create(params SqlScalarExpression[] items)
        {
            return new SqlLiteralArrayCollection(items);
        }

        public static SqlLiteralArrayCollection Create(IReadOnlyList<SqlScalarExpression> items)
        {
            return SqlLiteralArrayCollection.Create(items);
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

        public override void Accept(SqlCollectionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public override TResult Accept<TResult>(SqlCollectionVisitor<TResult> visitor)
        {
            return visitor.Visit(this);
        }

        public override TResult Accept<T, TResult>(SqlCollectionVisitor<T, TResult> visitor, T input)
        {
            return visitor.Visit(this, input);
        }
    }
}
