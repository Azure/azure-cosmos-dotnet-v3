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
    abstract class SqlPathExpression : SqlObject
    {
        protected SqlPathExpression(SqlPathExpression parentPath)
        {
            this.ParentPath = parentPath;
        }

        public SqlPathExpression ParentPath { get; }

        public abstract void Accept(SqlPathExpressionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlPathExpressionVisitor<TResult> visitor);
    }
}
