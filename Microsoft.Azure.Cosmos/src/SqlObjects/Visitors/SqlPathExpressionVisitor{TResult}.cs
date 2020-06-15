// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    internal abstract class SqlPathExpressionVisitor<TResult>
    {
        public abstract TResult Visit(SqlIdentifierPathExpression sqlObject);
        public abstract TResult Visit(SqlNumberPathExpression sqlObject);
        public abstract TResult Visit(SqlStringPathExpression sqlObject);
    }
}
