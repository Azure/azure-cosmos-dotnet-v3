//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlScalarExpressionVisitor
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
        public abstract void Visit(SqlLiteralScalarExpression scalarExpression);
        public abstract void Visit(SqlMemberIndexerScalarExpression scalarExpression);
        public abstract void Visit(SqlObjectCreateScalarExpression scalarExpression);
        public abstract void Visit(SqlParameterRefScalarExpression scalarExpression);
        public abstract void Visit(SqlPropertyRefScalarExpression scalarExpression);
        public abstract void Visit(SqlSubqueryScalarExpression scalarExpression);
        public abstract void Visit(SqlUnaryScalarExpression scalarExpression);
    }

    internal abstract class SqlScalarExpressionVisitor<TResult>
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
        public abstract TResult Visit(SqlLiteralScalarExpression scalarExpression);
        public abstract TResult Visit(SqlMemberIndexerScalarExpression scalarExpression);
        public abstract TResult Visit(SqlObjectCreateScalarExpression scalarExpression);
        public abstract TResult Visit(SqlParameterRefScalarExpression scalarExpression);
        public abstract TResult Visit(SqlPropertyRefScalarExpression scalarExpression);
        public abstract TResult Visit(SqlSubqueryScalarExpression scalarExpression);
        public abstract TResult Visit(SqlUnaryScalarExpression scalarExpression);
    }

    internal abstract class SqlScalarExpressionVisitor<TArg, TOutput>
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
        public abstract TOutput Visit(SqlLiteralScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlMemberIndexerScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlObjectCreateScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlParameterRefScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlPropertyRefScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlSubqueryScalarExpression scalarExpression, TArg input);
        public abstract TOutput Visit(SqlUnaryScalarExpression scalarExpression, TArg input);
    }
}
