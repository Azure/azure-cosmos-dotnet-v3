//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlProgram : SqlObject
    {
        private SqlProgram(SqlQuery query)
        {
            this.Query = query ?? throw new ArgumentNullException(nameof(query));
        }

        public SqlQuery Query { get; }

        public static SqlProgram Create(SqlQuery query) => new SqlProgram(query);

        public override void Accept(SqlObjectVisitor visitor) => visitor.Visit(this);

        public override TResult Accept<TResult>(SqlObjectVisitor<TResult> visitor) => visitor.Visit(this);

        public override TResult Accept<T, TResult>(SqlObjectVisitor<T, TResult> visitor, T input) => visitor.Visit(this, input);
    }
}
