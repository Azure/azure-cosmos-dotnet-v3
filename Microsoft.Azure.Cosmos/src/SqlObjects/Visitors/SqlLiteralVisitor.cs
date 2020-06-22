//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlLiteralVisitor
    {
        public abstract void Visit(SqlBooleanLiteral literal);
        public abstract void Visit(SqlNullLiteral literal);
        public abstract void Visit(SqlNumberLiteral literal);
        public abstract void Visit(SqlStringLiteral literal);
        public abstract void Visit(SqlUndefinedLiteral literal);
    }
}
