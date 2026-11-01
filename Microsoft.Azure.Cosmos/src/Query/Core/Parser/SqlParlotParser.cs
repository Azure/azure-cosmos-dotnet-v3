// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Globalization;
    using System.Text;
    using Microsoft.Azure.Cosmos.SqlObjects;

    /// <summary>
    /// Cosmos DB SQL parser. The grammar is in SqlParlotParser.parlot.cs and is compiled by the Parlot source generator,
    /// the methods below are the tokens and AST helpers the generated parser calls.
    /// </summary>
    /// <remarks>
    /// The scanners reproduce the tokens of the former ANTLR lexer (sql.g4): keywords are case insensitive except
    /// true, false, null, undefined and udf; a sign directly followed by a digit is part of a numeric literal; the only
    /// white space is space, tab, carriage return and line feed.
    /// </remarks>
    internal static partial class SqlParlotParser
    {
        public const string IdentifierStartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_";

        public const string IdentifierPartChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789";

        public const string NumberStartChars = "+-.0123456789";

        public static bool TryParse(string text, out SqlQuery sqlQuery) => TryParseCore(text, out sqlQuery);

        private static partial bool TryParseCore(string text, out SqlQuery sqlQuery);

        // LEX_IDENTIFIER that is not a reserved keyword (ALL, FIRST and LAST are keywords that remain identifiers).
        public static int ScanIdentifier(ReadOnlySpan<char> span)
        {
            int length = ScanWord(span);
            if (length == 0 || IsReserved(span.Slice(0, length)))
            {
                return 0;
            }

            return length;
        }

        // PARAMETER : '@' LEX_IDENTIFIER, where LEX_IDENTIFIER can be empty.
        public static int ScanParameter(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty || span[0] != '@')
            {
                return 0;
            }

            return 1 + ScanWord(span.Slice(1));
        }

        // NUMERIC_LITERAL : ('+' | '-')? DIGIT+ ('.' DIGIT*)? (E [-+]? DIGIT+)? | ('+' | '-')? '.' DIGIT+ (E [-+]? DIGIT+)?
        public static int ScanNumber(ReadOnlySpan<char> span)
        {
            int index = 0;
            if (index < span.Length && (span[index] == '+' || span[index] == '-'))
            {
                index++;
            }

            if (index < span.Length && IsDigit(span[index]))
            {
                index = SkipDigits(span, index);
                if (index < span.Length && span[index] == '.')
                {
                    index = SkipDigits(span, index + 1);
                }
            }
            else if (index + 1 < span.Length && span[index] == '.' && IsDigit(span[index + 1]))
            {
                index = SkipDigits(span, index + 1);
            }
            else
            {
                return 0;
            }

            if (index < span.Length && (span[index] == 'e' || span[index] == 'E'))
            {
                int exponent = index + 1;
                if (exponent < span.Length && (span[exponent] == '+' || span[exponent] == '-'))
                {
                    exponent++;
                }

                if (exponent < span.Length && IsDigit(span[exponent]))
                {
                    index = SkipDigits(span, exponent);
                }
            }

            return index;
        }

        // STRING_LITERAL : '"' (ESC | ~["\\\u0000-\u001F])* '"' | '\'' (ESC | ~['\\\u0000-\u001F])* '\''
        // ESC : '\\' (["\\/bfnrt] | 'u' HEX HEX HEX HEX)
        public static int ScanString(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty || (span[0] != '"' && span[0] != '\''))
            {
                return 0;
            }

            char quote = span[0];
            int index = 1;
            while (index < span.Length)
            {
                char c = span[index];
                if (c == quote)
                {
                    return index + 1;
                }

                if (c == '\\')
                {
                    if (index + 1 >= span.Length)
                    {
                        return 0;
                    }

                    switch (span[index + 1])
                    {
                        case '"':
                        case '\\':
                        case '/':
                        case 'b':
                        case 'f':
                        case 'n':
                        case 'r':
                        case 't':
                            index += 2;
                            break;
                        case 'u':
                            if (index + 5 >= span.Length
                                || !IsHex(span[index + 2])
                                || !IsHex(span[index + 3])
                                || !IsHex(span[index + 4])
                                || !IsHex(span[index + 5]))
                            {
                                return 0;
                            }

                            index += 6;
                            break;
                        default:
                            return 0;
                    }
                }
                else if (c < ' ')
                {
                    return 0;
                }
                else
                {
                    index++;
                }
            }

            return 0;
        }

        // '-' and '+' operators: the lexer reads a sign followed by a digit, or by '.' and a digit, as a numeric literal.
        public static int ScanMinus(ReadOnlySpan<char> span) => ScanSign(span, '-');

        public static int ScanPlus(ReadOnlySpan<char> span) => ScanSign(span, '+');

        public static SqlNumberLiteral CreateNumberLiteral(ReadOnlySpan<char> text)
        {
            // Same conversion as the former ANTLR visitor: long.TryParse (current culture), then double.Parse (invariant).
            if (text.Length <= 18)
            {
                long value = 0;
                int index = 0;
                while (index < text.Length && IsDigit(text[index]))
                {
                    value = (value * 10) + (text[index] - '0');
                    index++;
                }

                if (index == text.Length)
                {
                    return SqlNumberLiteral.Create(value);
                }
            }

            string number = text.ToString();
            if (long.TryParse(number, out long longValue))
            {
                return SqlNumberLiteral.Create(longValue);
            }

            return SqlNumberLiteral.Create(double.Parse(number, CultureInfo.InvariantCulture));
        }

        public static string GetStringValue(ReadOnlySpan<char> text)
        {
            // Removes the quotes and unescapes \" \\ and \/ only, keeping other escape sequences as written,
            // like the former ANTLR visitor.
            ReadOnlySpan<char> content = text.Slice(1, text.Length - 2);
            if (content.IndexOf('\\') < 0)
            {
                return content.ToString();
            }

            StringBuilder stringBuilder = new StringBuilder(content.Length);
            for (int index = 0; index < content.Length; index++)
            {
                char c = content[index];
                if (c == '\\' && index + 1 < content.Length)
                {
                    char next = content[index + 1];
                    if (next == '"' || next == '\\' || next == '/')
                    {
                        stringBuilder.Append(next);
                        index++;
                        continue;
                    }
                }

                stringBuilder.Append(c);
            }

            return stringBuilder.ToString();
        }

        public static ImmutableArray<T> ToImmutableArray<T>(IReadOnlyList<T> list)
        {
            if (list.Count == 0)
            {
                return ImmutableArray<T>.Empty;
            }

            ImmutableArray<T>.Builder builder = ImmutableArray.CreateBuilder<T>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                builder.Add(list[i]);
            }

            return builder.MoveToImmutable();
        }

        public static SqlScalarExpression ApplyPostfixes(SqlScalarExpression expression, IReadOnlyList<Postfix> postfixes)
        {
            for (int i = 0; i < postfixes.Count; i++)
            {
                Postfix postfix = postfixes[i];
                expression = postfix.Identifier != null
                    ? SqlPropertyRefScalarExpression.Create(expression, postfix.Identifier)
                    : SqlMemberIndexerScalarExpression.Create(expression, postfix.Index);
            }

            return expression;
        }

        public static SqlPathExpression CreatePath(IReadOnlyList<PathSegment> segments)
        {
            SqlPathExpression path = null;
            for (int i = 0; i < segments.Count; i++)
            {
                PathSegment segment = segments[i];
                if (segment.Identifier != null)
                {
                    path = SqlIdentifierPathExpression.Create(path, segment.Identifier);
                }
                else if (segment.Number != null)
                {
                    path = SqlNumberPathExpression.Create(path, segment.Number);
                }
                else
                {
                    path = SqlStringPathExpression.Create(path, segment.String);
                }
            }

            return path;
        }

        public static Leaf CreateLeaf(SqlScalarExpression expression, IReadOnlyList<Suffix> suffix)
        {
            if (suffix.Count == 0)
            {
                return new Leaf(expression, isBetween: false);
            }

            Suffix s = suffix[0];
            switch (s.Kind)
            {
                case SuffixKind.In:
                    return new Leaf(SqlInScalarExpression.Create(expression, s.Not, s.List), isBetween: false);
                case SuffixKind.Like:
                    return new Leaf(SqlLikeScalarExpression.Create(expression, s.First, s.Not, s.Escape), isBetween: false);
                default:
                    return new Leaf(SqlBetweenScalarExpression.Create(expression, s.First, s.Second, s.Not), isBetween: true);
            }
        }

        // logical_scalar_expression: AND binds tighter than OR, both are left associative.
        // A BETWEEN expression is not a logical_scalar_expression operand, so it can't be followed by AND / OR.
        public static bool IsValidLogical(Leaf first, IReadOnlyList<SqlScalarExpression> ands, IReadOnlyList<SqlScalarExpression> ors)
        {
            return !first.IsBetween || (ands.Count == 0 && ors.Count == 0);
        }

        public static SqlScalarExpression CreateLogical(Leaf first, IReadOnlyList<SqlScalarExpression> ands, IReadOnlyList<SqlScalarExpression> ors)
        {
            SqlScalarExpression expression = first.Expression;
            for (int i = 0; i < ands.Count; i++)
            {
                expression = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, expression, ands[i]);
            }

            for (int i = 0; i < ors.Count; i++)
            {
                expression = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Or, expression, ors[i]);
            }

            return expression;
        }

        private static int ScanSign(ReadOnlySpan<char> span, char sign)
        {
            if (span.IsEmpty || span[0] != sign)
            {
                return 0;
            }

            if (span.Length > 1 && (IsDigit(span[1]) || (span[1] == '.' && span.Length > 2 && IsDigit(span[2]))))
            {
                return 0;
            }

            return 1;
        }

        private static int ScanWord(ReadOnlySpan<char> span)
        {
            if (span.IsEmpty || !IsIdentifierStart(span[0]))
            {
                return 0;
            }

            int index = 1;
            while (index < span.Length && (IsIdentifierStart(span[index]) || IsDigit(span[index])))
            {
                index++;
            }

            return index;
        }

        private static bool IsReserved(ReadOnlySpan<char> word)
        {
            switch (word.Length)
            {
                case 2:
                    return EqualsIgnoreCase(word, "as") || EqualsIgnoreCase(word, "by") || EqualsIgnoreCase(word, "in") || EqualsIgnoreCase(word, "or");
                case 3:
                    return EqualsIgnoreCase(word, "and") || EqualsIgnoreCase(word, "asc") || EqualsIgnoreCase(word, "not") || EqualsIgnoreCase(word, "top")
                        || word.SequenceEqual("udf".AsSpan());
                case 4:
                    return EqualsIgnoreCase(word, "desc") || EqualsIgnoreCase(word, "from") || EqualsIgnoreCase(word, "join") || EqualsIgnoreCase(word, "left")
                        || EqualsIgnoreCase(word, "like") || EqualsIgnoreCase(word, "rank")
                        || word.SequenceEqual("true".AsSpan()) || word.SequenceEqual("null".AsSpan());
                case 5:
                    return EqualsIgnoreCase(word, "array") || EqualsIgnoreCase(word, "group") || EqualsIgnoreCase(word, "limit") || EqualsIgnoreCase(word, "order")
                        || EqualsIgnoreCase(word, "right") || EqualsIgnoreCase(word, "value") || EqualsIgnoreCase(word, "where")
                        || word.SequenceEqual("false".AsSpan());
                case 6:
                    return EqualsIgnoreCase(word, "escape") || EqualsIgnoreCase(word, "exists") || EqualsIgnoreCase(word, "offset") || EqualsIgnoreCase(word, "select");
                case 7:
                    return EqualsIgnoreCase(word, "between");
                case 8:
                    return EqualsIgnoreCase(word, "distinct");
                case 9:
                    return word.SequenceEqual("undefined".AsSpan());
                default:
                    return false;
            }
        }

        // The words only contain ASCII letters, digits and '_', and keyword is lower case.
        private static bool EqualsIgnoreCase(ReadOnlySpan<char> word, string keyword)
        {
            for (int i = 0; i < keyword.Length; i++)
            {
                if ((word[i] | 0x20) != keyword[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static int SkipDigits(ReadOnlySpan<char> span, int index)
        {
            while (index < span.Length && IsDigit(span[index]))
            {
                index++;
            }

            return index;
        }

        private static bool IsDigit(char c) => (uint)(c - '0') <= 9;

        private static bool IsIdentifierStart(char c) => (uint)((c | 0x20) - 'a') <= 'z' - 'a' || c == '_';

        private static bool IsHex(char c) => IsDigit(c) || (uint)((c | 0x20) - 'a') <= 'f' - 'a';

        public enum SuffixKind
        {
            In,
            Like,
            Between,
        }

        public readonly struct Postfix
        {
            public Postfix(SqlIdentifier identifier, SqlScalarExpression index)
            {
                this.Identifier = identifier;
                this.Index = index;
            }

            public SqlIdentifier Identifier { get; }

            public SqlScalarExpression Index { get; }
        }

        public readonly struct PathSegment
        {
            public PathSegment(SqlIdentifier identifier, SqlNumberLiteral number, SqlStringLiteral str)
            {
                this.Identifier = identifier;
                this.Number = number;
                this.String = str;
            }

            public SqlIdentifier Identifier { get; }

            public SqlNumberLiteral Number { get; }

            public SqlStringLiteral String { get; }
        }

        public readonly struct Suffix
        {
            public Suffix(SuffixKind kind, ImmutableArray<SqlScalarExpression> list, SqlScalarExpression first, SqlScalarExpression second, SqlStringLiteral escape)
            {
                this.Kind = kind;
                this.Not = false;
                this.List = list;
                this.First = first;
                this.Second = second;
                this.Escape = escape;
            }

            private Suffix(Suffix suffix, bool not)
            {
                this = suffix;
                this.Not = not;
            }

            public SuffixKind Kind { get; }

            public bool Not { get; }

            public ImmutableArray<SqlScalarExpression> List { get; }

            public SqlScalarExpression First { get; }

            public SqlScalarExpression Second { get; }

            public SqlStringLiteral Escape { get; }

            public Suffix WithNot(bool not) => new Suffix(this, not);
        }

        public readonly struct Leaf
        {
            public Leaf(SqlScalarExpression expression, bool isBetween)
            {
                this.Expression = expression;
                this.IsBetween = isBetween;
            }

            public SqlScalarExpression Expression { get; }

            public bool IsBetween { get; }
        }
    }
}
