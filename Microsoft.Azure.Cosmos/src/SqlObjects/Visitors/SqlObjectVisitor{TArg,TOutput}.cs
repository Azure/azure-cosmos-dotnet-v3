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
    abstract class SqlObjectVisitor<TArg, TOutput>
    {
        public abstract TOutput Visit(SqlAliasedCollectionExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlArrayCreateScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlArrayIteratorCollectionExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlArrayScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlBetweenScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlBinaryScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlBooleanLiteral sqlObject, TArg input);
        public abstract TOutput Visit(SqlCoalesceScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlConditionalScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlExistsScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlFromClause sqlObject, TArg input);
        public abstract TOutput Visit(SqlFunctionCallScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlGroupByClause sqlObject, TArg input);
        public abstract TOutput Visit(SqlIdentifier sqlObject, TArg input);
        public abstract TOutput Visit(SqlIdentifierPathExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlInputPathCollection sqlObject, TArg input);
        public abstract TOutput Visit(SqlInScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlJoinCollectionExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlLikeScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlLimitSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlLiteralScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlMemberIndexerScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlNullLiteral sqlObject, TArg input);
        public abstract TOutput Visit(SqlNumberLiteral sqlObject, TArg input);
        public abstract TOutput Visit(SqlNumberPathExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlObjectCreateScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlObjectProperty sqlObject, TArg input);
        public abstract TOutput Visit(SqlOffsetLimitClause sqlObject, TArg input);
        public abstract TOutput Visit(SqlOffsetSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlOrderByClause sqlObject, TArg input);
        public abstract TOutput Visit(SqlOrderByItem sqlObject, TArg input);
        public abstract TOutput Visit(SqlParameter sqlObject, TArg input);
        public abstract TOutput Visit(SqlParameterRefScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlProgram sqlObject, TArg input);
        public abstract TOutput Visit(SqlPropertyName sqlObject, TArg input);
        public abstract TOutput Visit(SqlPropertyRefScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlQuery sqlObject, TArg input);
        public abstract TOutput Visit(SqlSelectClause sqlObject, TArg input);
        public abstract TOutput Visit(SqlSelectItem sqlObject, TArg input);
        public abstract TOutput Visit(SqlSelectListSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlSelectStarSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlSelectValueSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlStringLiteral sqlObject, TArg input);
        public abstract TOutput Visit(SqlStringPathExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlSubqueryCollection sqlObject, TArg input);
        public abstract TOutput Visit(SqlSubqueryScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlTopSpec sqlObject, TArg input);
        public abstract TOutput Visit(SqlUnaryScalarExpression sqlObject, TArg input);
        public abstract TOutput Visit(SqlUndefinedLiteral sqlObject, TArg input);
        public abstract TOutput Visit(SqlWhereClause sqlObject, TArg input);
    }
}
