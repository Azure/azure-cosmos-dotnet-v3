//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal sealed class SqlObjectObfuscator : SqlObjectVisitor<SqlObject>
    {
        private static readonly HashSet<string> ExemptedString = new HashSet<string>()
        {
            "GeometryCollection",
            "LineString",
            "MultiLineString",
            "MultiPoint",
            "MultiPolygon",
            "Point",
            "Polygon",
            "_attachments",
            "_etag",
            "_rid",
            "_self",
            "_ts",
            "coordinates",
            "id",
            "name",
            "type"
        };

        private readonly Dictionary<string, string> obfuscatedStrings = new Dictionary<string, string>();
        private readonly Dictionary<Number64, Number64> obfuscatedNumbers = new Dictionary<Number64, Number64>();
        private int numberSequenceNumber;
        private int stringSequenceNumber;
        private int identifierSequenceNumber;
        private int fieldNameSequenceNumber;
        private int paramaterSequenceNumber;

        public override SqlObject Visit(SqlAliasedCollectionExpression sqlAliasedCollectionExpression)
        {
            return SqlAliasedCollectionExpression.Create(
                sqlAliasedCollectionExpression.Collection.Accept(this) as SqlCollection,
                sqlAliasedCollectionExpression.Alias.Accept(this) as SqlIdentifier);
        }

        public override SqlObject Visit(SqlArrayCreateScalarExpression sqlArrayCreateScalarExpression)
        {
            List<SqlScalarExpression> items = new List<SqlScalarExpression>();
            foreach (SqlScalarExpression item in sqlArrayCreateScalarExpression.Items)
            {
                items.Add(item.Accept(this) as SqlScalarExpression);
            }

            return SqlArrayCreateScalarExpression.Create(items.ToImmutableArray());
        }

        public override SqlObject Visit(SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
        {
            return SqlArrayIteratorCollectionExpression.Create(
                sqlArrayIteratorCollectionExpression.Identifier.Accept(this) as SqlIdentifier,
                sqlArrayIteratorCollectionExpression.Collection.Accept(this) as SqlCollection);
        }

        public override SqlObject Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
        {
            return SqlArrayScalarExpression.Create(sqlArrayScalarExpression.SqlQuery.Accept(this) as SqlQuery);
        }

        public override SqlObject Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
        {
            return SqlBetweenScalarExpression.Create(
                sqlBetweenScalarExpression.Expression.Accept(this) as SqlScalarExpression,
                sqlBetweenScalarExpression.StartInclusive.Accept(this) as SqlScalarExpression,
                sqlBetweenScalarExpression.EndInclusive.Accept(this) as SqlScalarExpression,
                sqlBetweenScalarExpression.Not);
        }

        public override SqlObject Visit(SqlBinaryScalarExpression sqlBinaryScalarExpression)
        {
            return SqlBinaryScalarExpression.Create(
                sqlBinaryScalarExpression.OperatorKind,
                sqlBinaryScalarExpression.LeftExpression.Accept(this) as SqlScalarExpression,
                sqlBinaryScalarExpression.RightExpression.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlBooleanLiteral sqlBooleanLiteral)
        {
            // booleans aren't PII so I will return as is.
            return sqlBooleanLiteral;
        }

        public override SqlObject Visit(SqlCoalesceScalarExpression sqlCoalesceScalarExpression)
        {
            return SqlCoalesceScalarExpression.Create(
                sqlCoalesceScalarExpression.Left.Accept(this) as SqlScalarExpression,
                sqlCoalesceScalarExpression.Right.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
        {
            return SqlConditionalScalarExpression.Create(
                sqlConditionalScalarExpression.Condition.Accept(this) as SqlScalarExpression,
                sqlConditionalScalarExpression.Consequent.Accept(this) as SqlScalarExpression,
                sqlConditionalScalarExpression.Alternative.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
        {
            return SqlExistsScalarExpression.Create(sqlExistsScalarExpression.Subquery.Accept(this) as SqlQuery);
        }

        public override SqlObject Visit(SqlFromClause sqlFromClause)
        {
            return SqlFromClause.Create(sqlFromClause.Expression.Accept(this) as SqlCollectionExpression);
        }

        public override SqlObject Visit(SqlFunctionCallScalarExpression sqlFunctionCallScalarExpression)
        {
            SqlScalarExpression[] items = new SqlScalarExpression[sqlFunctionCallScalarExpression.Arguments.Length];
            for (int i = 0; i < sqlFunctionCallScalarExpression.Arguments.Length; i++)
            {
                items[i] = sqlFunctionCallScalarExpression.Arguments[i].Accept(this) as SqlScalarExpression;
            }

            return SqlFunctionCallScalarExpression.Create(
                sqlFunctionCallScalarExpression.Name,
                sqlFunctionCallScalarExpression.IsUdf,
                items);
        }

        public override SqlObject Visit(SqlGroupByClause sqlGroupByClause)
        {
            SqlScalarExpression[] expressions = new SqlScalarExpression[sqlGroupByClause.Expressions.Length];
            for (int i = 0; i < sqlGroupByClause.Expressions.Length; i++)
            {
                expressions[i] = sqlGroupByClause.Expressions[i].Accept(this) as SqlScalarExpression;
            }

            return SqlGroupByClause.Create(expressions);
        }

        public override SqlObject Visit(SqlIdentifier sqlIdentifier)
        {
            return SqlIdentifier.Create(
                this.GetObfuscatedString(
                    sqlIdentifier.Value,
                    "ident",
                    ref this.identifierSequenceNumber));
        }

        public override SqlObject Visit(SqlIdentifierPathExpression sqlIdentifierPathExpression)
        {
            return SqlIdentifierPathExpression.Create(
                sqlIdentifierPathExpression.ParentPath?.Accept(this) as SqlPathExpression,
                sqlIdentifierPathExpression.Value.Accept(this) as SqlIdentifier);
        }

        public override SqlObject Visit(SqlInputPathCollection sqlInputPathCollection)
        {
            return SqlInputPathCollection.Create(
                sqlInputPathCollection.Input.Accept(this) as SqlIdentifier,
                sqlInputPathCollection.RelativePath?.Accept(this) as SqlPathExpression);
        }

        public override SqlObject Visit(SqlInScalarExpression sqlInScalarExpression)
        {
            SqlScalarExpression[] items = new SqlScalarExpression[sqlInScalarExpression.Haystack.Length];
            for (int i = 0; i < sqlInScalarExpression.Haystack.Length; i++)
            {
                items[i] = sqlInScalarExpression.Haystack[i].Accept(this) as SqlScalarExpression;
            }

            return SqlInScalarExpression.Create(
                sqlInScalarExpression.Needle.Accept(this) as SqlScalarExpression,
                sqlInScalarExpression.Not,
                items);
        }

        public override SqlObject Visit(SqlJoinCollectionExpression sqlJoinCollectionExpression)
        {
            return SqlJoinCollectionExpression.Create(
                sqlJoinCollectionExpression.Left.Accept(this) as SqlCollectionExpression,
                sqlJoinCollectionExpression.Right.Accept(this) as SqlCollectionExpression);
        }

        public override SqlObject Visit(SqlLikeScalarExpression sqlLikeScalarExpression)
        {
            return SqlLikeScalarExpression.Create(
                sqlLikeScalarExpression.Expression.Accept(this) as SqlScalarExpression,
                sqlLikeScalarExpression.Pattern.Accept(this) as SqlScalarExpression,
                sqlLikeScalarExpression.Not,
                sqlLikeScalarExpression.EscapeSequence?.Accept(this) as SqlStringLiteral);
        }

        public override SqlObject Visit(SqlLimitSpec sqlObject)
        {
            return SqlLimitSpec.Create(SqlNumberLiteral.Create(0));
        }

        public override SqlObject Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
        {
            return SqlLiteralScalarExpression.Create(sqlLiteralScalarExpression.Literal.Accept(this) as SqlLiteral);
        }

        public override SqlObject Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
        {
            return SqlMemberIndexerScalarExpression.Create(
                sqlMemberIndexerScalarExpression.Member.Accept(this) as SqlScalarExpression,
                sqlMemberIndexerScalarExpression.Indexer.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlNullLiteral sqlNullLiteral)
        {
            // null is not PII so I will just return it.
            return sqlNullLiteral;
        }

        public override SqlObject Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            return SqlNumberLiteral.Create(
                Number64.ToDouble(
                    this.GetObfuscatedNumber(sqlNumberLiteral.Value)));
        }

        public override SqlObject Visit(SqlNumberPathExpression sqlNumberPathExpression)
        {
            return SqlNumberPathExpression.Create(
                sqlNumberPathExpression.ParentPath?.Accept(this) as SqlPathExpression,
                sqlNumberPathExpression.Value.Accept(this) as SqlNumberLiteral);
        }

        public override SqlObject Visit(SqlObjectCreateScalarExpression sqlObjectCreateScalarExpression)
        {
            List<SqlObjectProperty> properties = new List<SqlObjectProperty>();
            foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
            {
                properties.Add(property.Accept(this) as SqlObjectProperty);
            }

            return SqlObjectCreateScalarExpression.Create(properties.ToImmutableArray());
        }

        public override SqlObject Visit(SqlObjectProperty sqlObjectProperty)
        {
            return SqlObjectProperty.Create(
                sqlObjectProperty.Name.Accept(this) as SqlPropertyName,
                sqlObjectProperty.Value.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlOffsetLimitClause sqlObject)
        {
            return SqlOffsetLimitClause.Create(
                sqlObject.OffsetSpec.Accept(this) as SqlOffsetSpec,
                sqlObject.LimitSpec.Accept(this) as SqlLimitSpec);
        }

        public override SqlObject Visit(SqlOffsetSpec sqlObject)
        {
            return SqlOffsetSpec.Create(SqlNumberLiteral.Create(0));
        }

        public override SqlObject Visit(SqlOrderByClause sqlOrderByClause)
        {
            SqlOrderByItem[] items = new SqlOrderByItem[sqlOrderByClause.OrderByItems.Length];
            for (int i = 0; i < sqlOrderByClause.OrderByItems.Length; i++)
            {
                items[i] = sqlOrderByClause.OrderByItems[i].Accept(this) as SqlOrderByItem;
            }

            return SqlOrderByClause.Create(items);
        }

        public override SqlObject Visit(SqlOrderByItem sqlOrderByItem)
        {
            return SqlOrderByItem.Create(
                sqlOrderByItem.Expression.Accept(this) as SqlScalarExpression,
                sqlOrderByItem.IsDescending);
        }

        public override SqlObject Visit(SqlParameter sqlParameter)
        {
            return SqlParameter.Create(
                this.GetObfuscatedString(
                    sqlParameter.Name,
                    "param",
                    ref this.paramaterSequenceNumber));
        }

        public override SqlObject Visit(SqlParameterRefScalarExpression sqlObject)
        {
            return SqlParameterRefScalarExpression.Create(sqlObject.Parameter.Accept(this) as SqlParameter);
        }

        public override SqlObject Visit(SqlProgram sqlProgram)
        {
            return SqlProgram.Create(sqlProgram.Query.Accept(this) as SqlQuery);
        }

        public override SqlObject Visit(SqlPropertyName sqlPropertyName)
        {
            return SqlPropertyName.Create(
                this.GetObfuscatedString(
                    sqlPropertyName.Value,
                    "p",
                    ref this.fieldNameSequenceNumber));
        }

        public override SqlObject Visit(SqlPropertyRefScalarExpression sqlPropertyRefScalarExpression)
        {
            return SqlPropertyRefScalarExpression.Create(
                sqlPropertyRefScalarExpression.Member?.Accept(this) as SqlScalarExpression,
                sqlPropertyRefScalarExpression.Identifier.Accept(this) as SqlIdentifier);
        }

        public override SqlObject Visit(SqlQuery sqlQuery)
        {
            return SqlQuery.Create(
                sqlQuery.SelectClause.Accept(this) as SqlSelectClause,
                sqlQuery.FromClause?.Accept(this) as SqlFromClause,
                sqlQuery.WhereClause?.Accept(this) as SqlWhereClause,
                sqlQuery.GroupByClause?.Accept(this) as SqlGroupByClause,
                sqlQuery.OrderByClause?.Accept(this) as SqlOrderByClause,
                sqlQuery.OffsetLimitClause?.Accept(this) as SqlOffsetLimitClause);
        }

        public override SqlObject Visit(SqlSelectClause sqlSelectClause)
        {
            return SqlSelectClause.Create(
                sqlSelectClause.SelectSpec.Accept(this) as SqlSelectSpec,
                sqlSelectClause.TopSpec?.Accept(this) as SqlTopSpec,
                sqlSelectClause.HasDistinct);
        }

        public override SqlObject Visit(SqlSelectItem sqlSelectItem)
        {
            return SqlSelectItem.Create(
                sqlSelectItem.Expression.Accept(this) as SqlScalarExpression,
                sqlSelectItem.Alias?.Accept(this) as SqlIdentifier);
        }

        public override SqlObject Visit(SqlSelectListSpec sqlSelectListSpec)
        {
            List<SqlSelectItem> items = new List<SqlSelectItem>();
            foreach (SqlSelectItem item in sqlSelectListSpec.Items)
            {
                items.Add(item.Accept(this) as SqlSelectItem);
            }

            return SqlSelectListSpec.Create(items.ToImmutableArray());
        }

        public override SqlObject Visit(SqlSelectStarSpec sqlSelectStarSpec)
        {
            // Not PII
            return sqlSelectStarSpec;
        }

        public override SqlObject Visit(SqlSelectValueSpec sqlSelectValueSpec)
        {
            return SqlSelectValueSpec.Create(sqlSelectValueSpec.Expression.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlStringLiteral sqlStringLiteral)
        {
            return SqlStringLiteral.Create(
                this.GetObfuscatedString(
                    sqlStringLiteral.Value,
                    "str",
                    ref this.stringSequenceNumber));
        }

        public override SqlObject Visit(SqlStringPathExpression sqlStringPathExpression)
        {
            return SqlStringPathExpression.Create(
                sqlStringPathExpression.ParentPath?.Accept(this) as SqlPathExpression,
                sqlStringPathExpression.Value.Accept(this) as SqlStringLiteral);
        }

        public override SqlObject Visit(SqlSubqueryCollection sqlSubqueryCollection)
        {
            return SqlSubqueryCollection.Create(sqlSubqueryCollection.Query.Accept(this) as SqlQuery);
        }

        public override SqlObject Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
        {
            return SqlSubqueryScalarExpression.Create(sqlSubqueryScalarExpression.Query.Accept(this) as SqlQuery);
        }

        public override SqlObject Visit(SqlTopSpec sqlTopSpec)
        {
            return SqlTopSpec.Create(SqlNumberLiteral.Create(0));
        }

        public override SqlObject Visit(SqlUnaryScalarExpression sqlUnaryScalarExpression)
        {
            return SqlUnaryScalarExpression.Create(
                sqlUnaryScalarExpression.OperatorKind,
                sqlUnaryScalarExpression.Expression.Accept(this) as SqlScalarExpression);
        }

        public override SqlObject Visit(SqlUndefinedLiteral sqlUndefinedLiteral)
        {
            // Not PII
            return sqlUndefinedLiteral;
        }

        public override SqlObject Visit(SqlWhereClause sqlWhereClause)
        {
            return SqlWhereClause.Create(sqlWhereClause.FilterExpression.Accept(this) as SqlScalarExpression);
        }

        private Number64 GetObfuscatedNumber(Number64 value)
        {
            Number64 obfuscatedNumber;
            // Leave NaN, Infinity, numbers in epsilon range, and small integers unchanged
            if (value.IsInfinity
                || (value.IsInteger && (Number64.ToLong(value) == long.MinValue))
                || (value.IsInteger && (Math.Abs(Number64.ToLong(value)) < 100))
                || (value.IsDouble && (Math.Abs(Number64.ToDouble(value)) < 100) && ((long)Number64.ToDouble(value) == Number64.ToDouble(value)))
                || (value.IsDouble && (Math.Abs(Number64.ToDouble(value)) <= double.Epsilon)))
            {
                obfuscatedNumber = value;
            }
            else
            {
                if (!this.obfuscatedNumbers.TryGetValue(value, out obfuscatedNumber))
                {
                    double doubleValue = Number64.ToDouble(value);

                    int sequenceNumber = ++this.numberSequenceNumber;

                    double log10 = Math.Floor(Math.Log10(Math.Abs(doubleValue)));
                    double adjustedSequence = Math.Pow(10, log10) * sequenceNumber / 1e4;

                    obfuscatedNumber = Math.Round(doubleValue, 2) + adjustedSequence;
                    this.obfuscatedNumbers.Add(value, obfuscatedNumber);
                }
            }

            return obfuscatedNumber;
        }

        private string GetObfuscatedString(string value, string prefix, ref int sequence)
        {
            // Leave short strings with length <= 1 unchanged.
            // Strings we generate have length >= 2, so there will not be collision.

            string obfuscatedString;
            if (value.Length <= 1)
            {
                obfuscatedString = value;
            }
            else if (SqlObjectObfuscator.ExemptedString.Contains(value))
            {
                obfuscatedString = value;
            }
            else
            {
                if (!this.obfuscatedStrings.TryGetValue(value, out obfuscatedString))
                {
                    int sequenceNumber = ++sequence; 
                    obfuscatedString = value.Length < 10 ? $"{prefix}{sequence}" : $"{prefix}{sequence}__{value.Length}";
                    this.obfuscatedStrings.Add(value, obfuscatedString);
                }
            }

            return obfuscatedString;
        }
    }
}
