//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SqlObjects.Visitors
{
    using System;
    using System.Buffers;
    using System.Buffers.Text;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;

    internal sealed class SqlObjectTextSerializer : SqlObjectVisitor
    {
        private const string Tab = "    ";
        private static readonly char[] CharactersThatNeedEscaping = Enumerable
            .Range(0, ' ')
            .Select(x => (char)x)
            .Concat(new char[] { '"', '\\' })
            .ToArray();
        private readonly StringWriter writer;
        private readonly bool prettyPrint;
        private int indentLevel;

        public SqlObjectTextSerializer(bool prettyPrint)
        {
            this.writer = new StringWriter(CultureInfo.InvariantCulture);
            this.prettyPrint = prettyPrint;
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
            int numberOfItems = sqlArrayCreateScalarExpression.Items.Count();
            if (numberOfItems == 0)
            {
                this.writer.Write("[]");
            }
            else if (numberOfItems == 1)
            {
                this.writer.Write("[");
                sqlArrayCreateScalarExpression.Items[0].Accept(this);
                this.writer.Write("]");
            }
            else
            {
                this.WriteStartContext("[");

                for (int i = 0; i < sqlArrayCreateScalarExpression.Items.Length; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlArrayCreateScalarExpression.Items[i].Accept(this);
                }

                this.WriteEndContext("]");
            }
        }

        public override void Visit(SqlArrayIteratorCollectionExpression sqlArrayIteratorCollectionExpression)
        {
            sqlArrayIteratorCollectionExpression.Identifier.Accept(this);
            this.writer.Write(" IN ");
            sqlArrayIteratorCollectionExpression.Collection.Accept(this);
        }

        public override void Visit(SqlArrayScalarExpression sqlArrayScalarExpression)
        {
            this.writer.Write("ARRAY");
            this.WriteStartContext("(");
            sqlArrayScalarExpression.SqlQuery.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlBetweenScalarExpression sqlBetweenScalarExpression)
        {
            this.writer.Write("(");
            sqlBetweenScalarExpression.Expression.Accept(this);

            if (sqlBetweenScalarExpression.Not)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" BETWEEN ");
            sqlBetweenScalarExpression.StartInclusive.Accept(this);
            this.writer.Write(" AND ");
            sqlBetweenScalarExpression.EndInclusive.Accept(this);
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
            sqlCoalesceScalarExpression.Left.Accept(this);
            this.writer.Write(" ?? ");
            sqlCoalesceScalarExpression.Right.Accept(this);
            this.writer.Write(")");
        }

        public override void Visit(SqlConditionalScalarExpression sqlConditionalScalarExpression)
        {
            this.writer.Write('(');
            sqlConditionalScalarExpression.Condition.Accept(this);
            this.writer.Write(" ? ");
            sqlConditionalScalarExpression.Consequent.Accept(this);
            this.writer.Write(" : ");
            sqlConditionalScalarExpression.Alternative.Accept(this);
            this.writer.Write(')');
        }

        public override void Visit(SqlExistsScalarExpression sqlExistsScalarExpression)
        {
            this.writer.Write("EXISTS");
            this.WriteStartContext("(");
            sqlExistsScalarExpression.Subquery.Accept(this);
            this.WriteEndContext(")");
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
            int numberOfArguments = sqlFunctionCallScalarExpression.Arguments.Count();
            if (numberOfArguments == 0)
            {
                this.writer.Write("()");
            }
            else if (numberOfArguments == 1)
            {
                this.writer.Write("(");
                sqlFunctionCallScalarExpression.Arguments[0].Accept(this);
                this.writer.Write(")");
            }
            else
            {
                this.WriteStartContext("(");

                for (int i = 0; i < sqlFunctionCallScalarExpression.Arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlFunctionCallScalarExpression.Arguments[i].Accept(this);
                }

                this.WriteEndContext(")");
            }
        }

        public override void Visit(SqlGroupByClause sqlGroupByClause)
        {
            this.writer.Write("GROUP BY ");
            sqlGroupByClause.Expressions[0].Accept(this);
            for (int i = 1; i < sqlGroupByClause.Expressions.Length; i++)
            {
                this.writer.Write(", ");
                sqlGroupByClause.Expressions[i].Accept(this);
            }
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
            }

            this.writer.Write(".");

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
            sqlInScalarExpression.Needle.Accept(this);
            if (sqlInScalarExpression.Not)
            {
                this.writer.Write(" NOT");
            }

            this.writer.Write(" IN ");

            int numberOfItems = sqlInScalarExpression.Haystack.Count();
            if (numberOfItems == 0)
            {
                this.writer.Write("()");
            }
            else if (numberOfItems == 1)
            {
                this.writer.Write("(");
                sqlInScalarExpression.Haystack[0].Accept(this);
                this.writer.Write(")");
            }
            else
            {
                this.WriteStartContext("(");

                for (int i = 0; i < sqlInScalarExpression.Haystack.Length; i++)
                {
                    if (i > 0)
                    {
                        this.WriteDelimiter(",");
                    }

                    sqlInScalarExpression.Haystack[i].Accept(this);
                }

                this.WriteEndContext(")");
            }
            this.writer.Write(")");
        }

        public override void Visit(SqlJoinCollectionExpression sqlJoinCollectionExpression)
        {
            sqlJoinCollectionExpression.Left.Accept(this);
            this.WriteNewline();
            this.WriteTab();
            this.writer.Write(" JOIN ");
            sqlJoinCollectionExpression.Right.Accept(this);
        }

        public override void Visit(SqlLimitSpec sqlObject)
        {
            this.writer.Write("LIMIT ");
            sqlObject.LimitExpression.Accept(this);
        }

        public override void Visit(SqlLikeScalarExpression sqlObject)
        {
            this.writer.Write("("); 

            sqlObject.Expression.Accept(this);

            if (sqlObject.Not)
            {
                this.writer.Write(" NOT ");
            }

            this.writer.Write(" LIKE ");

            sqlObject.Pattern.Accept(this);

            if (sqlObject.EscapeSequence != null)
            {
                this.writer.Write(" ESCAPE ");

                sqlObject.EscapeSequence.Accept(this);
            }

            this.writer.Write(")");
        }

        public override void Visit(SqlLiteralScalarExpression sqlLiteralScalarExpression)
        {
            sqlLiteralScalarExpression.Literal.Accept(this);
        }

        public override void Visit(SqlMemberIndexerScalarExpression sqlMemberIndexerScalarExpression)
        {
            sqlMemberIndexerScalarExpression.Member.Accept(this);
            this.writer.Write("[");
            sqlMemberIndexerScalarExpression.Indexer.Accept(this);
            this.writer.Write("]");
        }

        public override void Visit(SqlNullLiteral sqlNullLiteral)
        {
            this.writer.Write("null");
        }

        public override void Visit(SqlNumberLiteral sqlNumberLiteral)
        {
            SqlObjectTextSerializer.WriteNumber64(this.writer.GetStringBuilder(), sqlNumberLiteral.Value);
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
            int numberOfProperties = sqlObjectCreateScalarExpression.Properties.Count();
            if (numberOfProperties == 0)
            {
                this.writer.Write("{}");
            }
            else if (numberOfProperties == 1)
            {
                this.writer.Write("{");
                sqlObjectCreateScalarExpression.Properties.First().Accept(this);
                this.writer.Write("}");
            }
            else
            {
                this.WriteStartContext("{");
                bool firstItemProcessed = false;

                foreach (SqlObjectProperty property in sqlObjectCreateScalarExpression.Properties)
                {
                    if (firstItemProcessed)
                    {
                        this.WriteDelimiter(",");
                    }

                    property.Accept(this);
                    firstItemProcessed = true;
                }

                this.WriteEndContext("}");
            }
        }

        public override void Visit(SqlObjectProperty sqlObjectProperty)
        {
            sqlObjectProperty.Name.Accept(this);
            this.writer.Write(": ");
            sqlObjectProperty.Value.Accept(this);
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
            sqlObject.OffsetExpression.Accept(this);
        }

        public override void Visit(SqlOrderByClause sqlOrderByClause)
        {
            this.writer.Write("ORDER BY ");
            sqlOrderByClause.OrderByItems[0].Accept(this);

            for (int i = 1; i < sqlOrderByClause.OrderByItems.Length; i++)
            {
                this.writer.Write(", ");
                sqlOrderByClause.OrderByItems[i].Accept(this);
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

        public override void Visit(SqlParameter sqlParameter)
        {
            this.writer.Write(sqlParameter.Name);
        }

        public override void Visit(SqlParameterRefScalarExpression sqlParameterRefScalarExpression)
        {
            sqlParameterRefScalarExpression.Parameter.Accept(this);
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
            if (sqlPropertyRefScalarExpression.Member != null)
            {
                sqlPropertyRefScalarExpression.Member.Accept(this);
                this.writer.Write(".");
            }

            sqlPropertyRefScalarExpression.Identifier.Accept(this);
        }

        public override void Visit(SqlQuery sqlQuery)
        {
            sqlQuery.SelectClause.Accept(this);

            if (sqlQuery.FromClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.FromClause.Accept(this);
            }

            if (sqlQuery.WhereClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.WhereClause.Accept(this);
            }

            if (sqlQuery.GroupByClause != null)
            {
                sqlQuery.GroupByClause.Accept(this);
                this.writer.Write(" ");
            }

            if (sqlQuery.OrderByClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.OrderByClause.Accept(this);
            }

            if (sqlQuery.OffsetLimitClause != null)
            {
                this.WriteDelimiter(string.Empty);
                sqlQuery.OffsetLimitClause.Accept(this);
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
            int numberOfSelectSpecs = sqlSelectListSpec.Items.Count();
            if (numberOfSelectSpecs == 0)
            {
                throw new ArgumentException($"Expected {nameof(sqlSelectListSpec)} to have atleast 1 item.");
            }
            else if (numberOfSelectSpecs == 1)
            {
                sqlSelectListSpec.Items[0].Accept(this);
            }
            else
            {
                bool processedFirstItem = false;
                this.indentLevel++;
                this.WriteNewline();
                this.WriteTab();

                foreach (SqlSelectItem item in sqlSelectListSpec.Items)
                {
                    if (processedFirstItem)
                    {
                        this.WriteDelimiter(",");
                    }

                    item.Accept(this);
                    processedFirstItem = true;
                }

                this.indentLevel--;
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
            SqlObjectTextSerializer.WriteEscapedString(this.writer.GetStringBuilder(), sqlStringLiteral.Value.AsSpan());
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
            this.WriteStartContext("(");
            sqlSubqueryCollection.Query.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlSubqueryScalarExpression sqlSubqueryScalarExpression)
        {
            this.WriteStartContext("(");
            sqlSubqueryScalarExpression.Query.Accept(this);
            this.WriteEndContext(")");
        }

        public override void Visit(SqlTopSpec sqlTopSpec)
        {
            this.writer.Write("TOP ");
            sqlTopSpec.TopExpresion.Accept(this);
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

        private void WriteStartContext(string startCharacter)
        {
            this.indentLevel++;
            this.writer.Write(startCharacter);
            this.WriteNewline();
            this.WriteTab();
        }

        private void WriteDelimiter(string delimiter)
        {
            this.writer.Write(delimiter);
            this.writer.Write(' ');
            this.WriteNewline();
            this.WriteTab();
        }

        private void WriteEndContext(string endCharacter)
        {
            this.indentLevel--;
            this.WriteNewline();
            this.WriteTab();
            this.writer.Write(endCharacter);
        }

        private void WriteNewline()
        {
            if (this.prettyPrint)
            {
                this.writer.WriteLine();
            }
        }

        private void WriteTab()
        {
            if (this.prettyPrint)
            {
                for (int i = 0; i < this.indentLevel; i++)
                {
                    this.writer.Write(Tab);
                }
            }
        }

        private static unsafe void WriteNumber64(StringBuilder stringBuilder, Number64 value)
        {
            const int MaxNumberLength = 32;
            Span<byte> buffer = stackalloc byte[MaxNumberLength];
            if (value.IsInteger)
            {
                if (!Utf8Formatter.TryFormat(
                    value: Number64.ToLong(value),
                    destination: buffer,
                    bytesWritten: out int bytesWritten))
                {
                    throw new InvalidOperationException($"Failed to write a long.");
                }

                buffer = buffer.Slice(start: 0, length: bytesWritten);

                for (int i = 0; i < buffer.Length; i++)
                {
                    stringBuilder.Append((char)buffer[i]);
                }
            }
            else
            {
                // Until we move to Core 3.0 we have to call ToString(),
                // since neither G with precision nor R are supported for Utf8Formatter.
                stringBuilder.Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        private static unsafe void WriteEscapedString(StringBuilder stringBuilder, ReadOnlySpan<char> unescapedString)
        {
            while (!unescapedString.IsEmpty)
            {
                int? indexOfFirstCharacterThatNeedsEscaping = SqlObjectTextSerializer.IndexOfCharacterThatNeedsEscaping(unescapedString);
                if (!indexOfFirstCharacterThatNeedsEscaping.HasValue)
                {
                    // No escaping needed;
                    indexOfFirstCharacterThatNeedsEscaping = unescapedString.Length;
                }

                // Write as much of the string as possible
                ReadOnlySpan<char> noEscapeNeededPrefix = unescapedString.Slice(
                    start: 0,
                    length: indexOfFirstCharacterThatNeedsEscaping.Value);

                fixed (char* noEscapeNeedPrefixPointer = noEscapeNeededPrefix)
                {
                    stringBuilder.Append(noEscapeNeedPrefixPointer, noEscapeNeededPrefix.Length);
                }

                unescapedString = unescapedString.Slice(start: indexOfFirstCharacterThatNeedsEscaping.Value);

                // Escape the next character if it exists
                if (!unescapedString.IsEmpty)
                {
                    char character = unescapedString[0];
                    unescapedString = unescapedString.Slice(start: 1);

                    switch (character)
                    {
                        case '\\':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('\\');
                            break;

                        case '"':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('"');
                            break;

                        case '/':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('/');
                            break;

                        case '\b':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('b');
                            break;

                        case '\f':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('f');
                            break;

                        case '\n':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('n');
                            break;

                        case '\r':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('r');
                            break;

                        case '\t':
                            stringBuilder.Append('\\');
                            stringBuilder.Append('t');
                            break;

                        default:
                            char wideCharToEscape = character;
                            // We got a control character (U+0000 through U+001F).
                            stringBuilder.Append('\\');
                            stringBuilder.Append('u');
                            stringBuilder.Append(SqlObjectTextSerializer.GetHexDigit((wideCharToEscape >> 12) & 0xF));
                            stringBuilder.Append(SqlObjectTextSerializer.GetHexDigit((wideCharToEscape >> 8) & 0xF));
                            stringBuilder.Append(SqlObjectTextSerializer.GetHexDigit((wideCharToEscape >> 4) & 0xF));
                            stringBuilder.Append(SqlObjectTextSerializer.GetHexDigit((wideCharToEscape >> 0) & 0xF));
                            break;
                    }
                }
            }
        }

        private static int? IndexOfCharacterThatNeedsEscaping(ReadOnlySpan<char> unescapedString)
        {
            int? index = null;
            int indexOfAny = unescapedString.IndexOfAny(SqlObjectTextSerializer.CharactersThatNeedEscaping);
            if (indexOfAny != -1)
            {
                index = indexOfAny;
            }

            return index;
        }

        private static char GetHexDigit(int value)
        {
            return (char)((value < 10) ? '0' + value : 'A' + value - 10);
        }

        private static string SqlUnaryScalarOperatorKindToString(SqlUnaryScalarOperatorKind kind)
        {
            return kind switch
            {
                SqlUnaryScalarOperatorKind.BitwiseNot => "~",
                SqlUnaryScalarOperatorKind.Not => "NOT",
                SqlUnaryScalarOperatorKind.Minus => "-",
                SqlUnaryScalarOperatorKind.Plus => "+",
                _ => throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind)),
            };
        }

        private static string SqlBinaryScalarOperatorKindToString(SqlBinaryScalarOperatorKind kind)
        {
            return kind switch
            {
                SqlBinaryScalarOperatorKind.Add => "+",
                SqlBinaryScalarOperatorKind.And => "AND",
                SqlBinaryScalarOperatorKind.BitwiseAnd => "&",
                SqlBinaryScalarOperatorKind.BitwiseOr => "|",
                SqlBinaryScalarOperatorKind.BitwiseXor => "^",
                SqlBinaryScalarOperatorKind.Coalesce => "??",
                SqlBinaryScalarOperatorKind.Divide => "/",
                SqlBinaryScalarOperatorKind.Equal => "=",
                SqlBinaryScalarOperatorKind.GreaterThan => ">",
                SqlBinaryScalarOperatorKind.GreaterThanOrEqual => ">=",
                SqlBinaryScalarOperatorKind.LessThan => "<",
                SqlBinaryScalarOperatorKind.LessThanOrEqual => "<=",
                SqlBinaryScalarOperatorKind.Modulo => "%",
                SqlBinaryScalarOperatorKind.Multiply => "*",
                SqlBinaryScalarOperatorKind.NotEqual => "!=",
                SqlBinaryScalarOperatorKind.Or => "OR",
                SqlBinaryScalarOperatorKind.StringConcat => "||",
                SqlBinaryScalarOperatorKind.Subtract => "-",
                _ => throw new ArgumentException(string.Format(CultureInfo.CurrentUICulture, "Unsupported operator {0}", kind)),
            };
        }
    }
}
