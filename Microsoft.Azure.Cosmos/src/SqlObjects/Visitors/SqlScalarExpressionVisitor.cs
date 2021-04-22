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
    abstract class SqlScalarExpressionVisitor
    {
        public abstract void Visit(SqlArrayCreateScalarExpression scalarExpression);
        public abstract void Visit(SqlArrayScalarExpression scalarExpression);
        public abstract void Visit(SqlBetweenScalarExpression scalarExpression);
        public abstract void Visit(SqlBinaryScalarExpression scalarExpression);
        public abstract void Visit(SqlCoalesceScalarExpression scalarExpression);
        public abstract void Visit(SqlConditionalScalarExpression scalarExpression);
        public abstract void Visit(SqlExistsScalarExpression scalarExpression);
        public abstract void Visit(SqlFunctionCallScalarExpression scalarExpression);
        public abstract void Visit(SqlInScalarExpression scalarExpression);
        public abstract void Visit(SqlLikeScalarExpression scalarExpression);
        public abstract void Visit(SqlLiteralScalarExpression scalarExpression);
        public abstract void Visit(SqlMemberIndexerScalarExpression scalarExpression);
        public abstract void Visit(SqlObjectCreateScalarExpression scalarExpression);
        public abstract void Visit(SqlParameterRefScalarExpression scalarExpression);
        public abstract void Visit(SqlPropertyRefScalarExpression scalarExpression);
        public abstract void Visit(SqlSubqueryScalarExpression scalarExpression);
        public abstract void Visit(SqlUnaryScalarExpression scalarExpression);
    }
}
