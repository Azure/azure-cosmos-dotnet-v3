// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
#if INTERNAL
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
    public
#else
    internal
#endif
    abstract class SqlScalarExpressionVisitor<TResult>
    {
        public abstract TResult Visit(SqlArrayCreateScalarExpression scalarExpression);
        public abstract TResult Visit(SqlArrayScalarExpression scalarExpression);
        public abstract TResult Visit(SqlBetweenScalarExpression scalarExpression);
        public abstract TResult Visit(SqlBinaryScalarExpression scalarExpression);
        public abstract TResult Visit(SqlCoalesceScalarExpression scalarExpression);
        public abstract TResult Visit(SqlConditionalScalarExpression scalarExpression);
        public abstract TResult Visit(SqlExistsScalarExpression scalarExpression);
        public abstract TResult Visit(SqlFunctionCallScalarExpression scalarExpression);
        public abstract TResult Visit(SqlInScalarExpression scalarExpression);
        public abstract TResult Visit(SqlLikeScalarExpression scalarExpression);
        public abstract TResult Visit(SqlLiteralScalarExpression scalarExpression);
        public abstract TResult Visit(SqlMemberIndexerScalarExpression scalarExpression);
        public abstract TResult Visit(SqlObjectCreateScalarExpression scalarExpression);
        public abstract TResult Visit(SqlParameterRefScalarExpression scalarExpression);
        public abstract TResult Visit(SqlPropertyRefScalarExpression scalarExpression);
        public abstract TResult Visit(SqlSubqueryScalarExpression scalarExpression);
        public abstract TResult Visit(SqlUnaryScalarExpression scalarExpression);
    }
}
