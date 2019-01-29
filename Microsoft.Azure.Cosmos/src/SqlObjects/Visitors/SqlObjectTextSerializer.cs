//-----------------------------------------------------------------------------------------------------------------------------------------
// <copyright file="SqlObjectTextSerializer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Sql
{
    using Newtonsoft.Json;
    using System;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    internal sealed class SqlObjectTextSerializer : SqlObjectVisitor
    {
        private readonly StringWriter writer;

        public SqlObjectTextSerializer()
            : this(new StringBuilder(), CultureInfo.CurrentCulture)
        {
        }

        public SqlObjectTextSerializer(IFormatProvider formatProvider)
            : this(new StringBuilder(), formatProvider)
        {
        }

        public SqlObjectTextSerializer(StringBuilder stringBuilder)
            : this(stringBuilder, CultureInfo.CurrentCulture)
        {
        }

        public SqlObjectTextSerializer(StringBuilder stringBuilder, IFormatProvider formatProvider)
        {
            this.writer = new StringWriter(stringBuilder, formatProvider);
        }

        public override void Visit(SqlAliasedCollectionExpression sqlAliasedCollectionExpression)
        {
            sqlAliasedCollectionExpression.Collection.Accept(this);
            if (sqlAliasedCollectionExpression.Alias != null)
            {
                this.writer.Write(" AS ");
                sqlAliasedCollectionExpression.Alias.Accept(this);
            }
        }

        public override void Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
        {
            this.writer.Write("[");
            for (int i = 0; i < sqlArrayCreateScalarExpression.Items.Count; i++)
            {
                if (i > 0)
                {
                    this.writer.Write(", ");
                }

                sqlArrayCreateScalarExpression.Items[i].Accept(this);
            }

            this.writer.Write("]");
        }

        public override void Visit(SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
        {
            sqlArrayIteratorCollectionExpression.Alias.Accept(this);
            this.writer.Write(" IN ");
            sqlArrayIteratorCollectionExpression.Collection.Accept(this);
        }

        public override void Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
        {
            this.writer.Write("ARRAY");
            this.writer.Write("(");
            sqlArrayScalarExpression.SqlQuery.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
        {
            this.writer.Write("(");
            sqlBetweenScalarExpression.Expression.Accept(this);

            if (sqlBetweenScalarExpression.IsNot)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" BETWEEN ");
            sqlBetweenScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" AND ");
            sqlBetweenScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
        {
            this.writer.Write("(");
            sqlBinaryScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" ");
            this.writer.Write(SqlObjectTextSerializer.SqlBinaryScalarOperatorKindToString(sqlBinaryScalarExpression.OperatorKind));
            this.writer.Write(" ");
            sqlBinaryScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlBooleanLiteral sqlBooleanLiteral)
        {
            this.writer.Write(sqlBooleanLiteral.Value ? "true" : "false");
        }

        public override void Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
        {
            this.writer.Write("(");
            sqlCoalesceScalarExpression.LeftExpression.Accept(this);
            this.writer.Write(" ?? ");
            sqlCoalesceScalarExpression.RightExpression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
        {
            this.writer.Write('(');
            sqlConditionalScalarExpression.ConditionExpression.Accept(this);
            this.writer.Write(" ? ");
            sqlConditionalScalarExpression.FirstExpression.Accept(this);
            this.writer.Write(" : ");
            sqlConditionalScalarExpression.SecondExpression.Accept(this);
            this.writer.Write(')');
        }

        public override void Visit(SqlConversionScalarExpression sqlConversionScalarExpression)
        {
            sqlConversionScalarExpression.expression.Accept(this);
        }

        public override void Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
        {
            this.writer.Write("EXISTS");
            this.writer.Write("(");
            sqlExistsScalarExpression.SqlQuery.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlFromClause sqlFromClause)
        {
            this.writer.Write("FROM ");
            sqlFromClause.Expression.Accept(this);
        }

        public override void Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
        {
            if (sqlFunctionCallScalarExpression.IsUdf)
            {
                this.writer.Write("udf.");
            }

            sqlFunctionCallScalarExpression.Name.Accept(this);
            this.writer.Write("(");
            for (int i = 0; i < sqlFunctionCallScalarExpression.Arguments.Count; i++)
            {
                if (i > 0)
                {
                    this.writer.Write(", ");
                }

                sqlFunctionCallScalarExpression.Arguments[i].Accept(this);
            }

            this.writer.Write(")");
        }

        public override void Visit(SqlGeoNearCallScalarExpression sqlGeoNearCallScalarExpression)
        {
            this.writer.Write("(");
            this.writer.Write("_ST_DISTANCE");
            this.writer.Write("(");
            sqlGeoNearCallScalarExpression.PropertyRef.Accept(this);
            this.writer.Write(",");
            sqlGeoNearCallScalarExpression.Geometry.Accept(this);
            this.writer.Write(")");
            this.writer.Write(" BETWEEN ");

            if (sqlGeoNearCallScalarExpression.NumberOfPoints == null)
            {
                this.writer.Write(sqlGeoNearCallScalarExpression.MinimumDistance);
                this.writer.Write(" AND ");
                this.writer.Write(sqlGeoNearCallScalarExpression.MaximumDistance);
            }
            else
            {
                this.writer.Write(SqlGeoNearCallScalarExpression.NearMinimumDistanceName);
                this.writer.Write(" AND ");
                this.writer.Write(SqlGeoNearCallScalarExpression.NearMaximumDistanceName);
            }

            this.writer.Write(")");
        }

        public override void Visit(SqlIdentifier sqlIdentifier)
        {
            this.writer.Write(sqlIdentifier.Value);
        }

        public override void Visit(SqlIdentifierPathExpression sqlIdentifierPathExpression)
        {
            if (sqlIdentifierPathExpression.ParentPath != null)
            {
                sqlIdentifierPathExpression.ParentPath.Accept(this);
                this.writer.Write(".");
            }

            sqlIdentifierPathExpression.Value.Accept(this);
        }

        public override void Visit(SqlInputPathCollection sqlInputPathCollection)
        {
            sqlInputPathCollection.Input.Accept(this);
            if (sqlInputPathCollection.RelativePath != null)
            {
                sqlInputPathCollection.RelativePath.Accept(this);
            }
        }

        public override void Visit(SqlInScalarExpression sqlInScalarExpression)
        {
            this.writer.Write("(");
            sqlInScalarExpression.Expression.Accept(this);
            if (sqlInScalarExpression.Not)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" IN ");
            this.writer.Write("(");
            for (int i = 0; i < sqlInScalarExpression.Items.Count; i++)
            {
                if (i > 0)
                {
                    this.writer.Write(", ");
                }

                sqlInScalarExpression.Items[i].Accept(this);
            }

            this.writer.Write(")");
            this.writer.Write(")");
        }

        public override void Visit(SqlJoinCollectionExpression sqlJoinCollectionExpression)
        {
            sqlJoinCollectionExpression.LeftExpression.Accept(this);
            this.writer.Write(" JOIN ");
            sqlJoinCollectionExpression.RightExpression.Accept(this);
        }

        public override void Visit(SqlLimitSpec sqlObject)
        {
            this.writer.Write("LIMIT ");
            this.writer.Write(sqlObject.Limit);
        }

        public override void Visit(SqlLiteralArrayCollection sqlLiteralArrayCollection)
        {
            this.writer.Write("[");
            for (int i = 0; i < sqlLiteralArrayCollection.Items.Count; i++)
            {
                if (i > 0)
                {
                    this.writer.Write(", ");
                }

                sqlLiteralArrayCollection.Items[i].Accept(this);
            }
            this.writer.Write("]");
        }

        public override void Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
        {
            sqlLiteralScalarExpression.Literal.Accept(this);
        }

        public override void Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
        {
            sqlMemberIndexerScalarExpression.MemberExpression.Accept(this);
            this.writer.Write("[");
            sqlMemberIndexerScalarExpression.IndexExpression.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlNullLiteral sqlNullLiteral)
        {
            this.writer.Write("null");
        }

        public override void Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            this.writer.Write(sqlNumberLiteral.ToString());
        }

        public override void Visit(SqlNumberPathExpression sqlNumberPathExpression)
        {
            if (sqlNumberPathExpression.ParentPath != null)
            {
                sqlNumberPathExpression.ParentPath.Accept(this);
            }

            this.writer.Write("[");
            sqlNumberPathExpression.Value.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
        {
            this.writer.Write("{");
            bool firstItemProcessed = false;
            foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
            {
                if (firstItemProcessed)
                {
                    this.writer.Write(", ");
                }

                property.Accept(this);
                firstItemProcessed = true;
            }
            this.writer.Write("}");
        }

        public override void Visit(SqlObjectLiteral sqlObjectLiteral)
        {
            if (sqlObjectLiteral.isValueSerialized)
            {
                this.writer.Write(sqlObjectLiteral.Value);
            }
            else
            {
                this.writer.Write(JsonConvert.SerializeObject(sqlObjectLiteral.Value));
            }
        }

        public override void Visit(SqlObjectProperty sqlObjectProperty)
        {
            sqlObjectProperty.Name.Accept(this);
            this.writer.Write(": ");
            sqlObjectProperty.Expression.Accept(this);
        }

        public override void Visit(SqlOffsetLimitClause sqlObject)
        {
            sqlObject.OffsetSpec.Accept(this);
            this.writer.Write(" ");
            sqlObject.LimitSpec.Accept(this);
        }

        public override void Visit(SqlOffsetSpec sqlObject)
        {
            this.writer.Write("OFFSET ");
            this.writer.Write(sqlObject.Offset);
        }

        public override void Visit(SqlOrderbyClause sqlOrderByClause)
        {
            this.writer.Write("ORDER BY ");
            sqlOrderByClause.OrderbyItems[0].Accept(this);
            for (int i = 1; i < sqlOrderByClause.OrderbyItems.Count; i++)
            {
                this.writer.Write(", ");
                sqlOrderByClause.OrderbyItems[i].Accept(this);
            }
        }

        public override void Visit(SqlOrderByItem sqlOrderByItem)
        {
            sqlOrderByItem.Expression.Accept(this);
            if (sqlOrderByItem.IsDescending)
            {
                this.writer.Write(" DESC");
            }
            else
            {
                this.writer.Write(" ASC");
            }
        }

        public override void Visit(SqlProgram sqlProgram)
        {
            sqlProgram.Query.Accept(this);
        }

        public override void Visit(SqlPropertyName sqlPropertyName)
        {
            this.writer.Write('"');
            this.writer.Write(sqlPropertyName.Value);
            this.writer.Write('"');
        }

        public override void Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
        {
            if (sqlPropertyRefScalarExpression.MemberExpression != null)
            {
                sqlPropertyRefScalarExpression.MemberExpression.Accept(this);
                this.writer.Write(".");
            }

            sqlPropertyRefScalarExpression.PropertyIdentifier.Accept(this);
        }

        public override void Visit(SqlQuery sqlQuery)
        {
            sqlQuery.SelectClause.Accept(this);
            this.writer.Write(" ");

            if (sqlQuery.FromClause != null)
            {
                sqlQuery.FromClause.Accept(this);
                this.writer.Write(" ");
            }

            if (sqlQuery.WhereClause != null)
            {
                sqlQuery.WhereClause.Accept(this);
                this.writer.Write(" ");
            }

            if (sqlQuery.OrderbyClause != null)
            {
                sqlQuery.OrderbyClause.Accept(this);
                this.writer.Write(" ");
            }

            if (sqlQuery.OffsetLimitClause != null)
            {
                sqlQuery.OffsetLimitClause.Accept(this);
                this.writer.Write(" ");
            }
        }

        public override void Visit(SqlSelectClause sqlSelectClause)
        {
            this.writer.Write("SELECT ");

            if (sqlSelectClause.HasDistinct)
            {
                this.writer.Write("DISTINCT ");
            }

            if (sqlSelectClause.TopSpec != null)
            {
                sqlSelectClause.TopSpec.Accept(this);
                this.writer.Write(" ");
            }

            sqlSelectClause.SelectSpec.Accept(this);
        }

        public override void Visit(SqlSelectItem sqlSelectItem)
        {
            sqlSelectItem.Expression.Accept(this);
            if (sqlSelectItem.Alias != null)
            {
                this.writer.Write(" AS ");
                sqlSelectItem.Alias.Accept(this);
            }
        }

        public override void Visit(SqlSelectListSpec sqlSelectListSpec)
        {
            bool processedFirstItem = false;
            foreach (SqlSelectItem item in sqlSelectListSpec.Items)
            {
                if (processedFirstItem)
                {
                    this.writer.Write(", ");
                }

                item.Accept(this);
                processedFirstItem = true;
            }
        }

        public override void Visit(SqlSelectStarSpec sqlSelectStarSpec)
        {
            this.writer.Write("*");
        }

        public override void Visit(SqlSelectValueSpec sqlSelectValueSpec)
        {
            this.writer.Write("VALUE ");
            sqlSelectValueSpec.Expression.Accept(this);
        }

        public override void Visit(SqlStringLiteral sqlStringLiteral)
        {
            this.writer.Write("\"");

            string escapedString = GetEscapedString(sqlStringLiteral.Value);
            this.writer.Write(escapedString);

            this.writer.Write("\"");
        }

        public override void Visit(SqlStringPathExpression sqlStringPathExpression)
        {
            if (sqlStringPathExpression.ParentPath != null)
            {
                sqlStringPathExpression.ParentPath.Accept(this);
            }

            this.writer.Write("[");
            sqlStringPathExpression.Value.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlSubqueryCollection sqlSubqueryCollection)
        {
            this.writer.Write("(");
            sqlSubqueryCollection.Query.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
        {
            this.writer.Write("(");
            sqlSubqueryScalarExpression.Query.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlTopSpec sqlTopSpec)
        {
            this.writer.Write("TOP ");
            this.writer.Write(sqlTopSpec.Count);
        }

        public override void Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
        {
            this.writer.Write("(");
            this.writer.Write(SqlObjectTextSerializer.SqlUnaryScalarOperatorKindToString(sqlUnaryScalarExpression.OperatorKind));
            this.writer.Write(" ");
            sqlUnaryScalarExpression.Expression.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlUndefinedLiteral sqlUndefinedLiteral)
        {
            this.writer.Write("undefined");
        }

        public override void Visit(SqlWhereClause sqlWhereClause)
        {
            this.writer.Write("WHERE ");
            sqlWhereClause.FilterExpression.Accept(this);
        }

        public override string ToString()
        {
            return this.writer.ToString();
        }

        private static string GetEscapedString(string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }

            if (value.All(c => !IsEscapedCharacter(c)))
            {
                return value;
            }

            var stringBuilder = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        stringBuilder.Append("\\\"");
                        break;
                    case '\\':
                        stringBuilder.Append("\\\\");
                        break;
                    case '\b':
                        stringBuilder.Append("\\b");
                        break;
                    case '\f':
                        stringBuilder.Append("\\f");
                        break;
                    case '\n':
                        stringBuilder.Append("\\n");
                        break;
                    case '\r':
                        stringBuilder.Append("\\r");
                        break;
                    case '\t':
                        stringBuilder.Append("\\t");
                        break;
                    default:
                        switch (CharUnicodeInfo.GetUnicodeCategory(c))
                        {
                            case UnicodeCategory.UppercaseLetter:
                            case UnicodeCategory.LowercaseLetter:
                            case UnicodeCategory.TitlecaseLetter:
                            case UnicodeCategory.OtherLetter:
                            case UnicodeCategory.DecimalDigitNumber:
                            case UnicodeCategory.LetterNumber:
                            case UnicodeCategory.OtherNumber:
                            case UnicodeCategory.SpaceSeparator:
                            case UnicodeCategory.ConnectorPunctuation:
                            case UnicodeCategory.DashPunctuation:
                            case UnicodeCategory.OpenPunctuation:
                            case UnicodeCategory.ClosePunctuation:
                            case UnicodeCategory.InitialQuotePunctuation:
                            case UnicodeCategory.FinalQuotePunctuation:
                            case UnicodeCategory.OtherPunctuation:
                            case UnicodeCategory.MathSymbol:
                            case UnicodeCategory.CurrencySymbol:
                            case UnicodeCategory.ModifierSymbol:
                            case UnicodeCategory.OtherSymbol:
                                stringBuilder.Append(c);
                                break;
                            default:
                                stringBuilder.AppendFormat("\\u{0:x4}", (int)c);
                                break;
                        }
                        break;
                }
            }

            return stringBuilder.ToString();
        }

        private static bool IsEscapedCharacter(char c)
        {
            switch (c)
            {
                case '"':
                case '\\':
                case '\b':
                case '\f':
                case '\n':
                case '\r':
                case '\t':
                    return true;

                default:
                    switch (CharUnicodeInfo.GetUnicodeCategory(c))
                    {
                        case UnicodeCategory.UppercaseLetter:
                        case UnicodeCategory.LowercaseLetter:
                        case UnicodeCategory.TitlecaseLetter:
                        case UnicodeCategory.OtherLetter:
                        case UnicodeCategory.DecimalDigitNumber:
                        case UnicodeCategory.LetterNumber:
                        case UnicodeCategory.OtherNumber:
                        case UnicodeCategory.SpaceSeparator:
                        case UnicodeCategory.ConnectorPunctuation:
                        case UnicodeCategory.DashPunctuation:
                        case UnicodeCategory.OpenPunctuation:
                        case UnicodeCategory.ClosePunctuation:
                        case UnicodeCategory.InitialQuotePunctuation:
                        case UnicodeCategory.FinalQuotePunctuation:
                        case UnicodeCategory.OtherPunctuation:
                        case UnicodeCategory.MathSymbol:
                        case UnicodeCategory.CurrencySymbol:
                        case UnicodeCategory.ModifierSymbol:
                        case UnicodeCategory.OtherSymbol:
                            return false;

                        default:
                            return true;
                    }
            }
        }

        private static string SqlUnaryScalarOperatorKindToString(SqlUnaryScalarOperatorKind kind)
        {
            switch (kind)
            {
                case SqlUnaryScalarOperatorKind.BitwiseNot:
                    return "~";
                case SqlUnaryScalarOperatorKind.Not:
                    return "NOT";
                case SqlUnaryScalarOperatorKind.Minus:
                    return "-";
                case SqlUnaryScalarOperatorKind.Plus:
                    return "+";
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }

        private static string SqlBinaryScalarOperatorKindToString(SqlBinaryScalarOperatorKind kind)
        {
            switch (kind)
            {
                case SqlBinaryScalarOperatorKind.Add:
                    return "+";
                case SqlBinaryScalarOperatorKind.And:
                    return "AND";
                case SqlBinaryScalarOperatorKind.BitwiseAnd:
                    return "&";
                case SqlBinaryScalarOperatorKind.BitwiseOr:
                    return "|";
                case SqlBinaryScalarOperatorKind.BitwiseXor:
                    return "^";
                case SqlBinaryScalarOperatorKind.Coalesce:
                    return "??";
                case SqlBinaryScalarOperatorKind.Divide:
                    return "/";
                case SqlBinaryScalarOperatorKind.Equal:
                    return "=";
                case SqlBinaryScalarOperatorKind.GreaterThan:
                    return ">";
                case SqlBinaryScalarOperatorKind.GreaterThanOrEqual:
                    return ">=";
                case SqlBinaryScalarOperatorKind.LessThan:
                    return "<";
                case SqlBinaryScalarOperatorKind.LessThanOrEqual:
                    return "<=";
                case SqlBinaryScalarOperatorKind.Modulo:
                    return "%";
                case SqlBinaryScalarOperatorKind.Multiply:
                    return "*";
                case SqlBinaryScalarOperatorKind.NotEqual:
                    return "!=";
                case SqlBinaryScalarOperatorKind.Or:
                    return "OR";
                case SqlBinaryScalarOperatorKind.StringConcat:
                    return "||";
                case SqlBinaryScalarOperatorKind.Subtract:
                    return "-";
                default:
                    throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind));
            }
        }
    }
}
