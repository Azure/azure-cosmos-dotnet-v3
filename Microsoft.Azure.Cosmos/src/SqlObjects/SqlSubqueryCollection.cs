//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlSubqueryCollection : SqlCollection
    {
        private SqlSubqueryCollection(SqlQuery query)
        {
            this.Query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public SqlQuery Query { get; }

        public static SqlSubqueryCollection Create(SqlQuery query) => new SqlSubqueryCollection(query);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);

        public override void Accept(SqlCollectionVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlCollectionVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlCollectionVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
