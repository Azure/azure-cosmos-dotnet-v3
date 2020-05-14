//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlPathExpression : SqlObject
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
