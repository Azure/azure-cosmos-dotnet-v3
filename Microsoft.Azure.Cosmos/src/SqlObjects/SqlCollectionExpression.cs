//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlCollectionExpression : SqlObject
    {
        protected SqlCollectionExpression()
        {
        }

        public abstract void Accept(SqlCollectionExpressionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlCollectionExpressionVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlCollectionExpressionVisitor<T, TResult> visitor, T input);
    }
}
