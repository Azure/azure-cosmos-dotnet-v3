//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlScalarExpression : SqlObject
    {
        protected SqlScalarExpression()
        {
        }

        public abstract void Accept(SqlScalarExpressionVisitor visitor);

        public abstract TResult Accept<TResult>(SqlScalarExpressionVisitor<TResult> visitor);

        public abstract TResult Accept<T, TResult>(SqlScalarExpressionVisitor<T, TResult> visitor, T input);
    }
}
