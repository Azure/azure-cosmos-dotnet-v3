//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlSelectSpec : SqlObject
    {
        protected SqlSelectSpec()
        {
        }

        public abstract void Accept(SqlSelectSpecVisitor visitor);

        public abstract TResult Accept<TResult>(SqlSelectSpecVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlSelectSpecVisitor<T, TResult> visitor, T input);
    }
}
