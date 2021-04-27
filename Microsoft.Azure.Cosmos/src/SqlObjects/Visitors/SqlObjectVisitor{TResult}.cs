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
    abstract class SqlObjectVisitor<TResult>
    {
        public abstract TResult Visit(SqlAliasedCollectionExpression sqlObject);
        public abstract TResult Visit(SqlArrayCreateScalarExpression sqlObject);
        public abstract TResult Visit(SqlArrayIteratorCollectionExpression sqlObject);
        public abstract TResult Visit(SqlArrayScalarExpression sqlObject);
        public abstract TResult Visit(SqlBetweenScalarExpression sqlObject);
        public abstract TResult Visit(SqlBinaryScalarExpression sqlObject);
        public abstract TResult Visit(SqlBooleanLiteral sqlObject);
        public abstract TResult Visit(SqlCoalesceScalarExpression sqlObject);
        public abstract TResult Visit(SqlConditionalScalarExpression sqlObject);
        public abstract TResult Visit(SqlExistsScalarExpression sqlObject);
        public abstract TResult Visit(SqlFromClause sqlObject);
        public abstract TResult Visit(SqlFunctionCallScalarExpression sqlObject);
        public abstract TResult Visit(SqlGroupByClause sqlObject);
        public abstract TResult Visit(SqlIdentifier sqlObject);
        public abstract TResult Visit(SqlIdentifierPathExpression sqlObject);
        public abstract TResult Visit(SqlInputPathCollection sqlObject);
        public abstract TResult Visit(SqlJoinCollectionExpression sqlObject);
        public abstract TResult Visit(SqlLikeScalarExpression sqlObject);
        public abstract TResult Visit(SqlInScalarExpression sqlObject);
        public abstract TResult Visit(SqlLimitSpec sqlObject);
        public abstract TResult Visit(SqlLiteralScalarExpression sqlObject);
        public abstract TResult Visit(SqlMemberIndexerScalarExpression sqlObject);
        public abstract TResult Visit(SqlNullLiteral sqlObject);
        public abstract TResult Visit(SqlNumberLiteral sqlObject);
        public abstract TResult Visit(SqlNumberPathExpression sqlObject);
        public abstract TResult Visit(SqlObjectCreateScalarExpression sqlObject);
        public abstract TResult Visit(SqlObjectProperty sqlObject);
        public abstract TResult Visit(SqlOffsetLimitClause sqlObject);
        public abstract TResult Visit(SqlOffsetSpec sqlObject);
        public abstract TResult Visit(SqlOrderByClause sqlObject);
        public abstract TResult Visit(SqlOrderByItem sqlObject);
        public abstract TResult Visit(SqlParameter sqlObject);
        public abstract TResult Visit(SqlParameterRefScalarExpression sqlObject);
        public abstract TResult Visit(SqlProgram sqlObject);
        public abstract TResult Visit(SqlPropertyName sqlObject);
        public abstract TResult Visit(SqlPropertyRefScalarExpression sqlObject);
        public abstract TResult Visit(SqlQuery sqlObject);
        public abstract TResult Visit(SqlSelectClause sqlObject);
        public abstract TResult Visit(SqlSelectItem sqlObject);
        public abstract TResult Visit(SqlSelectListSpec sqlObject);
        public abstract TResult Visit(SqlSelectStarSpec sqlObject);
        public abstract TResult Visit(SqlSelectValueSpec sqlObject);
        public abstract TResult Visit(SqlStringLiteral sqlObject);
        public abstract TResult Visit(SqlStringPathExpression sqlObject);
        public abstract TResult Visit(SqlSubqueryCollection sqlObject);
        public abstract TResult Visit(SqlSubqueryScalarExpression sqlObject);
        public abstract TResult Visit(SqlTopSpec sqlObject);
        public abstract TResult Visit(SqlUnaryScalarExpression sqlObject);
        public abstract TResult Visit(SqlUndefinedLiteral sqlObject);
        public abstract TResult Visit(SqlWhereClause sqlObject);
    }
}
