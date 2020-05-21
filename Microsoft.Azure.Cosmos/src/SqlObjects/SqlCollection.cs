//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollection : SqlObject
    {
        protected SqlCollection()
        {
        }

        public abstract void Accept(SqlCollectionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlCollectionVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlCollectionVisitor<T, TResult> visitor, T input);
    }
}
