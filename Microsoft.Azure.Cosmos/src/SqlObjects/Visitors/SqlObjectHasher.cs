//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using System;
    using System.Globalization;

    internal sealed class SqlObjectHasher : SqlObjectVisitor<int>
    {
        public static readonly SqlObjectHasher Singleton = new SqlObjectHasher(true);

        private const int SqlAliasedCollectionExpressionHashCode = 1202039781;
        private const int SqlArrayCreateScalarExpressionHashCode = 1760950661;
        private const int SqlArrayIteratorCollectionExpressionHashCode = -468874086;
        private const int SqlArrayScalarExpressionHashCode = -1093553293;
        private const int SqlBetweenScalarExpressionHashCode = -943872277;
        private const int SqlBetweenScalarExpressionNotHashCode = -1283200473;
        private const int SqlBinaryScalarExpressionHashCode = 1667146665;
        private const int SqlBooleanLiteralHashCode = 739161617;
        private const int SqlBooleanLiteralTrueHashCode = 1545461565;
        private const int SqlBooleanLiteralFalseHashCode = -2072875075;
        private const int SqlCoalesceScalarExpressionHashCode = -1400659633;
        private const int SqlConditionalScalarExpressionHashCode = -421337832;
        private const int SqlExistsScalarExpressionHashCode = 1168675587;
        private const int SqlFromClauseHashCode = 52588336;
        private const int SqlFunctionCallScalarExpressionHashCode = 496783446;
        private const int SqlFunctionCallScalarExpressionUdfHashCode = 1547906315;
        private const int SqlGroupByClauseHashCode = 130396242;
        private const int SqlIdentifierHashCode = -1664307981;
        private const int SqlIdentifierPathExpressionHashCode = -1445813508;
        private const int SqlInputPathCollectionHashCode = -209963066;
        private const int SqlInScalarExpressionHashCode = 1439386783;
        private const int SqlInScalarExpressionNotHashCode = -1131398119;
        private const int SqlJoinCollectionExpressionHashCode = 1000382226;
        private const int SqlLikeScalarExpressionHashCode = 317861;
        private const int SqlLimitSpecHashCode = 92601316;
        private const int SqlLiteralScalarExpressionHashCode = -158339101;
        private const int SqlMemberIndexerScalarExpressionHashCode = 1589675618;
        private const int SqlNullLiteralHashCode = -709456592;
        private const int SqlNumberLiteralHashCode = 159836309;
        private const int SqlNumberPathExpressionHashCode = 874210976;
        private const int SqlObjectCreateScalarExpressionHashCode = -131129165;
        private const int SqlObjectPropertyHashCode = 1218972715;
        private const int SqlOffsetLimitClauseHashCode = 150154755;
        private const int SqlOffsetSpecHashCode = 109062001;
        private const int SqlOrderbyClauseHashCode = 1361708336;
        private const int SqlOrderbyItemHashCode = 846566057;
        private const int SqlOrderbyItemAscendingHashCode = -1123129997;
        private const int SqlOrderbyItemDescendingHashCode = -703648622;
        private const int SqlParameterHashCode = -1853999792;
        private const int SqlParameterRefScalarExpressionHashCode = 1446117758;
        private const int SqlProgramHashCode = -492711050;
        private const int SqlPropertyNameHashCode = 1262661966;
        private const int SqlPropertyRefScalarExpressionHashCode = -1586896865;
        private const int SqlQueryHashCode = 1968642960;
        private const int SqlSelectClauseHashCode = 19731870;
        private const int SqlSelectClauseDistinctHashCode = 1467616881;
        private const int SqlSelectItemHashCode = -611151157;
        private const int SqlSelectListSpecHashCode = -1704039197;
        private const int SqlSelectStarSpecHashCode = -1125875092;
        private const int SqlSelectValueSpecHashCode = 507077368;
        private const int SqlStringLiteralHashCode = -1542874155;
        private const int SqlStringPathExpressionHashCode = -1280625326;
        private const int SqlSubqueryCollectionHashCode = 1175697100;
        private const int SqlSubqueryScalarExpressionHashCode = -1327458193;
        private const int SqlTopSpecHashCode = -791376698;
        private const int SqlUnaryScalarExpressionHashCode = 723832597;
        private const int SqlUndefinedLiteralHashCode = 1290712518;
        private const int SqlWhereClauseHashCode = -516465563;

        private static class SqlBinaryScalarOperatorKindHashCodes
        {
            public const int Add = 977447154;
            public const int And = -539169937;
            public const int BitwiseAnd = 192594476;
            public const int BitwiseOr = -1494193777;
            public const int BitwiseXor = 140893802;
            public const int Coalesce = -461857726;
            public const int Divide = -1486745780;
            public const int Equal = -69389992;
            public const int GreaterThan = 1643533106;
            public const int GreaterThanOrEqual = 180538014;
            public const int LessThan = -1452081072;
            public const int LessThanOrEqual = -1068434012;
            public const int Modulo = -371220256;
            public const int Multiply = -178990484;
            public const int NotEqual = 65181046;
            public const int Or = -2095255335;
            public const int StringConcat = -525384764;
            public const int Subtract = 2070749634;
        }

        private static class SqlUnaryScalarOperatorKindHashCodes
        {
            public const int BitwiseNot = 1177827907;
            public const int Not = 1278008063;
            public const int Minus = -1942284846;
            public const int Plus = 251767493;
        }

        private readonly bool isStrict;

        public SqlObjectHasher(bool isStrict)
        {
            this.isStrict = isStrict;
        }

        public override int Visit(SqlAliasedCollectionExpression sqlAliasedCollectionExpression)
        {
            int hashCode = SqlAliasedCollectionExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlAliasedCollectionExpression.Collection.Accept(this));
            if (sqlAliasedCollectionExpression.Alias != null)
            {
                hashCode = CombineHashes(hashCode, sqlAliasedCollectionExpression.Alias.Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
        {
            int hashCode = SqlArrayCreateScalarExpressionHashCode;
            for (int i = 0; i < sqlArrayCreateScalarExpression.Items.Length; i++)
            {
                hashCode = CombineHashes(hashCode, sqlArrayCreateScalarExpression.Items[i].Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
        {
            int hashCode = SqlArrayIteratorCollectionExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlArrayIteratorCollectionExpression.Identifier.Accept(this));
            hashCode = CombineHashes(hashCode, sqlArrayIteratorCollectionExpression.Collection.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
        {
            int hashCode = SqlArrayScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlArrayScalarExpression.SqlQuery.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
        {
            int hashCode = SqlBetweenScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlBetweenScalarExpression.Expression.Accept(this));
            if (sqlBetweenScalarExpression.Not)
            {
                hashCode = SqlObjectHasher.CombineHashes(hashCode, SqlBetweenScalarExpressionNotHashCode);
            }

            hashCode = CombineHashes(hashCode, sqlBetweenScalarExpression.StartInclusive.Accept(this));
            hashCode = CombineHashes(hashCode, sqlBetweenScalarExpression.EndInclusive.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
        {
            int hashCode = SqlBinaryScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlBinaryScalarExpression.LeftExpression.Accept(this));
            hashCode = CombineHashes(hashCode, SqlObjectHasher.SqlBinaryScalarOperatorKindGetHashCode(sqlBinaryScalarExpression.OperatorKind));
            hashCode = CombineHashes(hashCode, sqlBinaryScalarExpression.RightExpression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlBooleanLiteral sqlBooleanLiteral)
        {
            int hashCode = SqlBooleanLiteralHashCode;
            hashCode = CombineHashes(hashCode, sqlBooleanLiteral.Value ? SqlBooleanLiteralTrueHashCode : SqlBooleanLiteralFalseHashCode);
            return hashCode;
        }

        public override int Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
        {
            int hashCode = SqlCoalesceScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlCoalesceScalarExpression.Left.Accept(this));
            hashCode = CombineHashes(hashCode, sqlCoalesceScalarExpression.Right.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
        {
            int hashCode = SqlConditionalScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlConditionalScalarExpression.Condition.Accept(this));
            hashCode = CombineHashes(hashCode, sqlConditionalScalarExpression.Consequent.Accept(this));
            hashCode = CombineHashes(hashCode, sqlConditionalScalarExpression.Alternative.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
        {
            int hashCode = SqlExistsScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlExistsScalarExpression.Subquery.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlFromClause sqlFromClause)
        {
            int hashCode = SqlFromClauseHashCode;
            hashCode = CombineHashes(hashCode, sqlFromClause.Expression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
        {
            int hashCode = SqlFunctionCallScalarExpressionHashCode;
            if (sqlFunctionCallScalarExpression.IsUdf)
            {
                hashCode = CombineHashes(hashCode, SqlFunctionCallScalarExpressionUdfHashCode);
            }

            hashCode = CombineHashes(hashCode, sqlFunctionCallScalarExpression.Name.Accept(this));
            for (int i = 0; i < sqlFunctionCallScalarExpression.Arguments.Length; i++)
            {
                hashCode = CombineHashes(hashCode, sqlFunctionCallScalarExpression.Arguments[i].Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlGroupByClause sqlGroupByClause)
        {
            int hashCode = SqlGroupByClauseHashCode;
            for (int i = 0; i < sqlGroupByClause.Expressions.Length; i++)
            {
                hashCode = CombineHashes(hashCode, sqlGroupByClause.Expressions[i].Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlIdentifier sqlIdentifier)
        {
            int hashCode = SqlIdentifierHashCode;
            hashCode = CombineHashes(hashCode, SqlObjectHasher.Djb2(sqlIdentifier.Value));
            return hashCode;
        }

        public override int Visit(SqlIdentifierPathExpression sqlIdentifierPathExpression)
        {
            int hashCode = SqlIdentifierPathExpressionHashCode;
            if (sqlIdentifierPathExpression.ParentPath != null)
            {
                hashCode = CombineHashes(hashCode, sqlIdentifierPathExpression.ParentPath.Accept(this));
            }

            hashCode = CombineHashes(hashCode, sqlIdentifierPathExpression.Value.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlInputPathCollection sqlInputPathCollection)
        {
            int hashCode = SqlInputPathCollectionHashCode;
            hashCode = CombineHashes(hashCode, sqlInputPathCollection.Input.Accept(this));
            if (sqlInputPathCollection.RelativePath != null)
            {
                hashCode = CombineHashes(hashCode, sqlInputPathCollection.RelativePath.Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlInScalarExpression sqlInScalarExpression)
        {
            int hashCode = SqlInScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlInScalarExpression.Needle.Accept(this));
            if (sqlInScalarExpression.Not)
            {
                hashCode = CombineHashes(hashCode, SqlInScalarExpressionNotHashCode);
            }

            for (int i = 0; i < sqlInScalarExpression.Haystack.Length; i++)
            {
                hashCode = CombineHashes(hashCode, sqlInScalarExpression.Haystack[i].Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlJoinCollectionExpression sqlJoinCollectionExpression)
        {
            int hashCode = SqlJoinCollectionExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlJoinCollectionExpression.Left.Accept(this));
            hashCode = CombineHashes(hashCode, sqlJoinCollectionExpression.Right.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlLimitSpec sqlObject)
        {
            int hashCode = SqlLimitSpecHashCode;
            hashCode = CombineHashes(hashCode, sqlObject.LimitExpression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlLikeScalarExpression sqlLikeScalarExpression)
        {
            int hashCode = SqlLikeScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlLikeScalarExpression.Expression.Accept(this));
            hashCode = CombineHashes(hashCode, sqlLikeScalarExpression.Not ? 1 : 0);
            hashCode = CombineHashes(hashCode, sqlLikeScalarExpression.Pattern.Accept(this));
            if (sqlLikeScalarExpression.EscapeSequence != null)
            {
                hashCode = CombineHashes(hashCode, sqlLikeScalarExpression.EscapeSequence.Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
        {
            int hashCode = SqlLiteralScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlLiteralScalarExpression.Literal.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
        {
            int hashCode = SqlMemberIndexerScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlMemberIndexerScalarExpression.Member.Accept(this));
            hashCode = CombineHashes(hashCode, sqlMemberIndexerScalarExpression.Indexer.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlNullLiteral sqlNullLiteral)
        {
            return SqlNullLiteralHashCode;
        }

        public override int Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            int hashCode = SqlNumberLiteralHashCode;
            hashCode = CombineHashes(hashCode, sqlNumberLiteral.Value.GetHashCode());
            return hashCode;
        }

        public override int Visit(SqlNumberPathExpression sqlNumberPathExpression)
        {
            int hashCode = SqlNumberPathExpressionHashCode;
            if (sqlNumberPathExpression.ParentPath != null)
            {
                hashCode = CombineHashes(hashCode, sqlNumberPathExpression.ParentPath.Accept(this));
            }

            hashCode = CombineHashes(hashCode, sqlNumberPathExpression.Value.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
        {
            int hashCode = SqlObjectCreateScalarExpressionHashCode;
            foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
            {
                if (this.isStrict)
                {
                    hashCode = CombineHashes(hashCode, property.Accept(this));
                }
                else
                {
                    // Combining the hash functions needs to be done in a symmetric manner
                    // since order of object properties does not change equality.
                    hashCode += property.Accept(this);
                }
            }

            return hashCode;
        }

        public override int Visit(SqlObjectProperty sqlObjectProperty)
        {
            int hashCode = SqlObjectPropertyHashCode;
            hashCode = CombineHashes(hashCode, sqlObjectProperty.Name.Accept(this));
            hashCode = CombineHashes(hashCode, sqlObjectProperty.Value.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlOffsetLimitClause sqlObject)
        {
            int hashCode = SqlOffsetLimitClauseHashCode;
            hashCode = CombineHashes(hashCode, sqlObject.OffsetSpec.Accept(this));
            hashCode = CombineHashes(hashCode, sqlObject.LimitSpec.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlOffsetSpec sqlObject)
        {
            int hashCode = SqlOffsetSpecHashCode;
            hashCode = CombineHashes(hashCode, sqlObject.OffsetExpression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlOrderByClause sqlOrderByClause)
        {
            int hashCode = SqlOrderbyClauseHashCode;
            for (int i = 0; i < sqlOrderByClause.OrderByItems.Length; i++)
            {
                hashCode = CombineHashes(hashCode, sqlOrderByClause.OrderByItems[i].Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlOrderByItem sqlOrderByItem)
        {
            int hashCode = SqlOrderbyItemHashCode;
            hashCode = CombineHashes(hashCode, sqlOrderByItem.Expression.Accept(this));
            if (sqlOrderByItem.IsDescending)
            {
                hashCode = CombineHashes(hashCode, SqlOrderbyItemDescendingHashCode);
            }
            else
            {
                hashCode = CombineHashes(hashCode, SqlOrderbyItemAscendingHashCode);
            }

            return hashCode;
        }

        public override int Visit(SqlParameter sqlObject)
        {
            int hashCode = SqlParameterHashCode;
            hashCode = CombineHashes(hashCode, SqlObjectHasher.Djb2(sqlObject.Name));
            return hashCode;
        }

        public override int Visit(SqlParameterRefScalarExpression sqlObject)
        {
            int hashCode = SqlParameterRefScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlObject.Parameter.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlProgram sqlProgram)
        {
            int hashCode = SqlProgramHashCode;
            hashCode = CombineHashes(hashCode, sqlProgram.Query.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlPropertyName sqlPropertyName)
        {
            int hashCode = SqlPropertyNameHashCode;
            hashCode = CombineHashes(hashCode, SqlObjectHasher.Djb2(sqlPropertyName.Value));
            return hashCode;
        }

        public override int Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
        {
            int hashCode = SqlPropertyRefScalarExpressionHashCode;
            if (sqlPropertyRefScalarExpression.Member != null)
            {
                hashCode = CombineHashes(hashCode, sqlPropertyRefScalarExpression.Member.Accept(this));
            }

            hashCode = CombineHashes(hashCode, sqlPropertyRefScalarExpression.Identifier.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlQuery sqlQuery)
        {
            int hashCode = SqlQueryHashCode;
            hashCode = CombineHashes(hashCode, sqlQuery.SelectClause.Accept(this));

            if (sqlQuery.FromClause != null)
            {
                hashCode = CombineHashes(hashCode, sqlQuery.FromClause.Accept(this));
            }

            if (sqlQuery.WhereClause != null)
            {
                hashCode = CombineHashes(hashCode, sqlQuery.WhereClause.Accept(this));
            }

            if (sqlQuery.GroupByClause != null)
            {
                hashCode = CombineHashes(hashCode, sqlQuery.GroupByClause.Accept(this));
            }

            if (sqlQuery.OrderByClause != null)
            {
                hashCode = CombineHashes(hashCode, sqlQuery.OrderByClause.Accept(this));
            }

            if (sqlQuery.OffsetLimitClause != null)
            {
                hashCode = CombineHashes(hashCode, sqlQuery.OffsetLimitClause.Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlSelectClause sqlSelectClause)
        {
            int hashCode = SqlSelectClauseHashCode;
            if (sqlSelectClause.HasDistinct)
            {
                hashCode = CombineHashes(hashCode, SqlSelectClauseDistinctHashCode);
            }

            if (sqlSelectClause.TopSpec != null)
            {
                hashCode = CombineHashes(hashCode, sqlSelectClause.TopSpec.Accept(this));
            }

            hashCode = CombineHashes(hashCode, sqlSelectClause.SelectSpec.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlSelectItem sqlSelectItem)
        {
            int hashCode = SqlSelectItemHashCode;
            hashCode = CombineHashes(hashCode, sqlSelectItem.Expression.Accept(this));
            if (sqlSelectItem.Alias != null)
            {
                hashCode = CombineHashes(hashCode, sqlSelectItem.Alias.Accept(this));
            }

            return hashCode;
        }

        public override int Visit(SqlSelectListSpec sqlSelectListSpec)
        {
            int hashCode = SqlSelectListSpecHashCode;
            foreach (SqlSelectItem item in sqlSelectListSpec.Items)
            {
                if (this.isStrict)
                {
                    hashCode = CombineHashes(hashCode, item.Accept(this));
                }
                else
                {
                    // order of select items does not affect equality
                    // so the hash function needs to be symmetric.
                    hashCode += item.Accept(this);
                }
            }

            return hashCode;
        }

        public override int Visit(SqlSelectStarSpec sqlSelectStarSpec)
        {
            return SqlSelectStarSpecHashCode;
        }

        public override int Visit(SqlSelectValueSpec sqlSelectValueSpec)
        {
            int hashCode = SqlSelectValueSpecHashCode;
            hashCode = CombineHashes(hashCode, sqlSelectValueSpec.Expression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlStringLiteral sqlStringLiteral)
        {
            int hashCode = SqlStringLiteralHashCode;
            hashCode = CombineHashes(hashCode, SqlObjectHasher.Djb2(sqlStringLiteral.Value));
            return hashCode;
        }

        public override int Visit(SqlStringPathExpression sqlStringPathExpression)
        {
            int hashCode = SqlStringPathExpressionHashCode;
            if (sqlStringPathExpression.ParentPath != null)
            {
                hashCode = CombineHashes(hashCode, sqlStringPathExpression.ParentPath.Accept(this));
            }

            hashCode = CombineHashes(hashCode, sqlStringPathExpression.Value.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlSubqueryCollection sqlSubqueryCollection)
        {
            int hashCode = SqlSubqueryCollectionHashCode;
            hashCode = CombineHashes(hashCode, sqlSubqueryCollection.Query.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
        {
            int hashCode = SqlSubqueryScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, sqlSubqueryScalarExpression.Query.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlTopSpec sqlTopSpec)
        {
            int hashCode = SqlTopSpecHashCode;
            hashCode = CombineHashes(hashCode, sqlTopSpec.TopExpresion.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
        {
            int hashCode = SqlUnaryScalarExpressionHashCode;
            hashCode = CombineHashes(hashCode, SqlObjectHasher.SqlUnaryScalarOperatorKindGetHashCode(sqlUnaryScalarExpression.OperatorKind));
            hashCode = CombineHashes(hashCode, sqlUnaryScalarExpression.Expression.Accept(this));
            return hashCode;
        }

        public override int Visit(SqlUndefinedLiteral sqlUndefinedLiteral)
        {
            return SqlUndefinedLiteralHashCode;
        }

        public override int Visit(SqlWhereClause sqlWhereClause)
        {
            int hashCode = SqlWhereClauseHashCode;
            hashCode = CombineHashes(hashCode, sqlWhereClause.FilterExpression.Accept(this));
            return hashCode;
        }

        private static int SqlUnaryScalarOperatorKindGetHashCode(SqlUnaryScalarOperatorKind kind)
        {
            return kind switch
            {
                SqlUnaryScalarOperatorKind.BitwiseNot => SqlUnaryScalarOperatorKindHashCodes.BitwiseNot,
                SqlUnaryScalarOperatorKind.Not => SqlUnaryScalarOperatorKindHashCodes.Not,
                SqlUnaryScalarOperatorKind.Minus => SqlUnaryScalarOperatorKindHashCodes.Minus,
                SqlUnaryScalarOperatorKind.Plus => SqlUnaryScalarOperatorKindHashCodes.Plus,
                _ => throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind)),
            };
        }

        private static int SqlBinaryScalarOperatorKindGetHashCode(SqlBinaryScalarOperatorKind kind)
        {
            return kind switch
            {
                SqlBinaryScalarOperatorKind.Add => SqlBinaryScalarOperatorKindHashCodes.Add,
                SqlBinaryScalarOperatorKind.And => SqlBinaryScalarOperatorKindHashCodes.And,
                SqlBinaryScalarOperatorKind.BitwiseAnd => SqlBinaryScalarOperatorKindHashCodes.BitwiseAnd,
                SqlBinaryScalarOperatorKind.BitwiseOr => SqlBinaryScalarOperatorKindHashCodes.BitwiseOr,
                SqlBinaryScalarOperatorKind.BitwiseXor => SqlBinaryScalarOperatorKindHashCodes.BitwiseXor,
                SqlBinaryScalarOperatorKind.Coalesce => SqlBinaryScalarOperatorKindHashCodes.Coalesce,
                SqlBinaryScalarOperatorKind.Divide => SqlBinaryScalarOperatorKindHashCodes.Divide,
                SqlBinaryScalarOperatorKind.Equal => SqlBinaryScalarOperatorKindHashCodes.Equal,
                SqlBinaryScalarOperatorKind.GreaterThan => SqlBinaryScalarOperatorKindHashCodes.GreaterThan,
                SqlBinaryScalarOperatorKind.GreaterThanOrEqual => SqlBinaryScalarOperatorKindHashCodes.GreaterThanOrEqual,
                SqlBinaryScalarOperatorKind.LessThan => SqlBinaryScalarOperatorKindHashCodes.LessThan,
                SqlBinaryScalarOperatorKind.LessThanOrEqual => SqlBinaryScalarOperatorKindHashCodes.LessThanOrEqual,
                SqlBinaryScalarOperatorKind.Modulo => SqlBinaryScalarOperatorKindHashCodes.Modulo,
                SqlBinaryScalarOperatorKind.Multiply => SqlBinaryScalarOperatorKindHashCodes.Multiply,
                SqlBinaryScalarOperatorKind.NotEqual => SqlBinaryScalarOperatorKindHashCodes.NotEqual,
                SqlBinaryScalarOperatorKind.Or => SqlBinaryScalarOperatorKindHashCodes.Or,
                SqlBinaryScalarOperatorKind.StringConcat => SqlBinaryScalarOperatorKindHashCodes.StringConcat,
                SqlBinaryScalarOperatorKind.Subtract => SqlBinaryScalarOperatorKindHashCodes.Subtract,
                _ => throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind)),
            };
        }

        /// <summary>
        /// Combines Two Hashes in an antisymmetric way (stolen from boost).
        /// </summary>
        /// <param name="lhs">The first hash</param>
        /// <param name="rhs">The second hash</param>
        /// <returns>The combined hash.</returns>
        private static int CombineHashes(long lhs, long rhs)
        {
            lhs ^= rhs + 0x9e3779b9 + (lhs << 6) + (lhs >> 2);
            return (int)lhs;
        }

        private static int Djb2(string value)
        {
            ulong hash = 5381;
            ulong c;
            for (int i = 0; i < value.Length; i++)
            {
                c = value[i];
                hash = (hash << 5) + hash + c; /* hash * 33 + c */
            }

            return (int)hash;
        }
    }
}
