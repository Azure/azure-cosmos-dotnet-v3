//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects
{
    using Microsoft.Azure.Cosmos.SqlObjects.Visitors;

#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlCollection : SqlObject
    {
        protected SqlCollection()
        {
        }

        public abstract void Accept(SqlCollectionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlCollectionVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlCollectionVisitor<T, TResult> visitor, T input);
    }
}
