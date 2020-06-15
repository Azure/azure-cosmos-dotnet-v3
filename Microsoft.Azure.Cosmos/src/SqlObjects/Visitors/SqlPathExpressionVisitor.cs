//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    internal abstract class SqlPathExpressionVisitor
    {
        public abstract void Visit(SqlIdentifierPathExpression sqlObject);
        public abstract void Visit(SqlNumberPathExpression sqlObject);
        public abstract void Visit(SqlStringPathExpression sqlObject);
    }
}
