//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlPathExpressionVisitor
    {
        public abstract void Visit(SqlIdentifierPathExpression sqlObject);
        public abstract void Visit(SqlNumberPathExpression sqlObject);
        public abstract void Visit(SqlStringPathExpression sqlObject);
    }

    internal abstract class SqlPathExpressionVisitor<TResult>
    {
        public abstract TResult Visit(SqlIdentifierPathExpression sqlObject);
        public abstract TResult Visit(SqlNumberPathExpression sqlObject);
        public abstract TResult Visit(SqlStringPathExpression sqlObject);
    }
}
