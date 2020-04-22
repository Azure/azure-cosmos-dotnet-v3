//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using System;

    internal sealed class SqlProgram : SqlObject
    {
        private SqlProgram(SqlQuery query)
            : base(SqlObjectKind.Program)
        {
            this.Query = query ?? throw new ArgumentNullException($"{nameof(query)} must not be null.");
        }

        public SqlQuery Query
        {
            get;
        }

        public static SqlProgram Create(SqlQuery query)
        {
            return new SqlProgram(query);
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
