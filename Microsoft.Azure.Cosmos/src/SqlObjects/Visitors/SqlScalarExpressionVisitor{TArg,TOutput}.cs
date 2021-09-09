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
    abstract class SqlScalarExpressionVisitor<TArg, TOutput>
    {
        public abstract TOutput Visit(SqlArrayCreateScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlArrayScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlBetweenScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlBinaryScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlCoalesceScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlConditionalScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlExistsScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlFunctionCallScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlInScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlLikeScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlLiteralScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlMemberIndexerScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlObjectCreateScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlParameterRefScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlPropertyRefScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlSubqueryScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlUnaryScalarExpression scalarExpression, TArg input);
    }
}
