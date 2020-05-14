//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    internal abstract class SqlObjectVisitor
    {
        public abstract void Visit(SqlAliasedCollectionExpression sqlObject);
        public abstract void Visit(SqlArrayCreateScalarExpression sqlObject);
        public abstract void Visit(SqlArrayIteratorCollectionExpression sqlObject);
        public abstract void Visit(SqlArrayScalarExpression sqlObject);
        public abstract void Visit(SqlBetweenScalarExpression sqlObject);
        public abstract void Visit(SqlBinaryScalarExpression sqlObject);
        public abstract void Visit(SqlBooleanLiteral sqlObject);
        public abstract void Visit(SqlCoalesceScalarExpression sqlObject);
        public abstract void Visit(SqlConditionalScalarExpression sqlObject);
        public abstract void Visit(SqlExistsScalarExpression sqlObject);
        public abstract void Visit(SqlFromClause sqlObject);
        public abstract void Visit(SqlFunctionCallScalarExpression sqlObject);
        public abstract void Visit(SqlGroupByClause sqlObject);
        public abstract void Visit(SqlIdentifier sqlObject);
        public abstract void Visit(SqlIdentifierPathExpression sqlObject);
        public abstract void Visit(SqlInputPathCollection sqlObject);
        public abstract void Visit(SqlInScalarExpression sqlObject);
        public abstract void Visit(SqlLimitSpec sqlObject);
        public abstract void Visit(SqlJoinCollectionExpression sqlObject);
        public abstract void Visit(SqlLiteralScalarExpression sqlObject);
        public abstract void Visit(SqlMemberIndexerScalarExpression sqlObject);
        public abstract void Visit(SqlNullLiteral sqlObject);
        public abstract void Visit(SqlNumberLiteral sqlObject);
        public abstract void Visit(SqlNumberPathExpression sqlObject);
        public abstract void Visit(SqlObjectCreateScalarExpression sqlObject);
        public abstract void Visit(SqlObjectProperty sqlObject);
        public abstract void Visit(SqlOffsetLimitClause sqlObject);
        public abstract void Visit(SqlOffsetSpec sqlObject);
        public abstract void Visit(SqlOrderbyClause sqlObject);
        public abstract void Visit(SqlOrderByItem sqlObject);
        public abstract void Visit(SqlParameter sqlObject);
        public abstract void Visit(SqlParameterRefScalarExpression sqlObject);
        public abstract void Visit(SqlProgram sqlObject);
        public abstract void Visit(SqlPropertyName sqlObject);
        public abstract void Visit(SqlPropertyRefScalarExpression sqlObject);
        public abstract void Visit(SqlQuery sqlObject);
        public abstract void Visit(SqlSelectClause sqlObject);
        public abstract void Visit(SqlSelectItem sqlObject);
        public abstract void Visit(SqlSelectListSpec sqlObject);
        public abstract void Visit(SqlSelectStarSpec sqlObject);
        public abstract void Visit(SqlSelectValueSpec sqlObject);
        public abstract void Visit(SqlStringLiteral sqlObject);
        public abstract void Visit(SqlStringPathExpression sqlObject);
        public abstract void Visit(SqlSubqueryCollection sqlObject);
        public abstract void Visit(SqlSubqueryScalarExpression sqlObject);
        public abstract void Visit(SqlTopSpec sqlObject);
        public abstract void Visit(SqlUnaryScalarExpression sqlObject);
        public abstract void Visit(SqlUndefinedLiteral sqlObject);
        public abstract void Visit(SqlWhereClause sqlObject);
    }

    internal abstract class SqlObjectVisitor<TResult>
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
        public abstract TResult Visit(SqlInScalarExpression sqlObject);
        public abstract TResult Visit(SqlLimitSpec sqlObject);
        public abstract TResult Visit(SqlJoinCollectionExpression sqlObject);
        public abstract TResult Visit(SqlLiteralScalarExpression sqlObject);
        public abstract TResult Visit(SqlMemberIndexerScalarExpression sqlObject);
        public abstract TResult Visit(SqlNullLiteral sqlObject);
        public abstract TResult Visit(SqlNumberLiteral sqlObject);
        public abstract TResult Visit(SqlNumberPathExpression sqlObject);
        public abstract TResult Visit(SqlObjectCreateScalarExpression sqlObject);
        public abstract TResult Visit(SqlObjectProperty sqlObject);
        public abstract TResult Visit(SqlOffsetLimitClause sqlObject);
        public abstract TResult Visit(SqlOffsetSpec sqlObject);
        public abstract TResult Visit(SqlOrderbyClause sqlObject);
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

    internal abstract class SqlObjectVisitor<TArg, TOutput>
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
        public abstract TOutput Visit(SqlOrderbyClause sqlObject, TArg input);
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
