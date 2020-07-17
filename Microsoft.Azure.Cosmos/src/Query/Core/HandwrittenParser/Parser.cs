// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.HandwrittenParser
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects;

    internal static class Parser
    {
        public static SqlQuery Parse(ReadOnlySpan<char> text)
        {
            Scanner scanner = new Scanner(text);
            return ParseQuery(ref scanner);
        }

        private static SqlQuery ParseQuery(ref Scanner scanner)
        {
            SqlSelectClause sqlSelectClause = ParseSelectClause(ref scanner);
            SqlFromClause sqlFromClause = scanner.Token == TokenKind.FromKeyword ? ParseFromClause(ref scanner) : default;
            SqlWhereClause sqlWhereClause = scanner.Token == TokenKind.WhereKeyword ? ParseWhereClause(ref scanner) : default;
            SqlGroupByClause sqlGroupByClause = scanner.Token == TokenKind.GroupKeyword ? ParseGroupByClause(ref scanner) : default;
            SqlOrderByClause sqlOrderByClause = scanner.Token == TokenKind.OrderKeyword ? ParseOrderByClause(ref scanner) : default;
            SqlOffsetLimitClause sqlOffsetLimitClause = scanner.Token == TokenKind.OffsetKeyword ? ParseOffsetLimitClause(ref scanner) : default;

            ParseExpected(TokenKind.EOF, ref scanner);

            return SqlQuery.Create(
                sqlSelectClause,
                sqlFromClause,
                sqlWhereClause,
                sqlGroupByClause,
                sqlOrderByClause,
                sqlOffsetLimitClause);
        }

        private static SqlSelectClause ParseSelectClause(ref Scanner scanner)
        {
            if (scanner.Token != TokenKind.SelectKeyword)
            {
                throw new ParseException("Invalid query - must start with `select`");
            }

            scanner.Scan();

            bool hasDistinct;
            if (scanner.Token == TokenKind.DistinctKeyword)
            {
                hasDistinct = true;
                scanner.Scan();
            }
            else
            {
                hasDistinct = false;
            }

            SqlTopSpec topSpec;
            if (scanner.Token == TokenKind.TopKeyword)
            {
                scanner.Scan();

                if (scanner.Token != TokenKind.NumericLiteral)
                {
                    throw new ParseException("The TOP clause requires a number, e.g. `SELECT TOP 10`");
                }

                Number64 topCount = ParseNumber(ref scanner);

                topSpec = SqlTopSpec.Create(SqlNumberLiteral.Create(topCount));
            }
            else
            {
                topSpec = default;
            }

            SqlSelectSpec sqlSelectSpec = ParseSelectSpec(ref scanner);

            return SqlSelectClause.Create(sqlSelectSpec, topSpec, hasDistinct);
        }

        private static SqlSelectSpec ParseSelectSpec(ref Scanner scanner)
        {
            if (scanner.Token == TokenKind.Star)
            {
                scanner.Scan();
                return SqlSelectStarSpec.Singleton;
            }

            if (scanner.Token == TokenKind.ValueKeyword)
            {
                scanner.Scan();
                SqlScalarExpression scalarExpression = ParseScalarExpression(ref scanner);
                return SqlSelectValueSpec.Create(scalarExpression);
            }

            return ParseSelectListSpec(ref scanner);
        }

        private static SqlSelectListSpec ParseSelectListSpec(ref Scanner scanner)
        {
            List<SqlSelectItem> selectItems = new List<SqlSelectItem>();
            while (scanner.Token == TokenKind.Comma)
            {
                scanner.Scan();
                selectItems.Add(ParseSelectItem(ref scanner));
            }

            return SqlSelectListSpec.Create(selectItems);
        }

        private static SqlSelectItem ParseSelectItem(ref Scanner scanner)
        {
            SqlScalarExpression sqlScalarExpression = ParseScalarExpression(ref scanner);
            SqlIdentifier sqlIdentifier;
            if (scanner.Token == TokenKind.AsKeyword)
            {
                scanner.Scan();
                sqlIdentifier = ParseIdentifier(ref scanner);
            }
            else
            {
                sqlIdentifier = default;
            }

            return SqlSelectItem.Create(sqlScalarExpression, sqlIdentifier);
        }

        private static SqlFromClause ParseFromClause(ref Scanner scanner)
        {
            ParseExpected(TokenKind.FromKeyword, ref scanner);

            SqlCollectionExpression sqlCollectionExpression = ParseJoinCollectionExpression(ref scanner);
            return SqlFromClause.Create(sqlCollectionExpression);
        }

        private static SqlCollectionExpression ParseJoinCollectionExpression(ref Scanner scanner)
        {
            SqlCollectionExpression expression = ParseCollectionExpression(ref scanner);

            while (scanner.Token == TokenKind.JoinKeyword)
            {
                scanner.Scan();
                SqlCollectionExpression rhsExpression = ParseCollectionExpression(ref scanner);

                return SqlJoinCollectionExpression.Create(expression, rhsExpression);
            }
            return expression;
        }

        private static SqlCollectionExpression ParseCollectionExpression(ref Scanner scanner)
        {
            SqlCollection collection = ParseCollection(ref scanner);

            if (scanner.Token == TokenKind.AsKeyword)
            {
                scanner.Scan();
                SqlIdentifier identifier = ParseIdentifier(ref scanner);
                return SqlAliasedCollectionExpression.Create(collection, identifier);
            }

            throw new InvalidOperationException();
        }

        private static SqlCollection ParseCollection(ref Scanner scanner)
        {
            throw new NotImplementedException();
        }

        private static SqlWhereClause ParseWhereClause(ref Scanner scanner)
        {
            ParseExpected(TokenKind.WhereKeyword, ref scanner);
            SqlScalarExpression sqlScalarExpression = ParseScalarExpression(ref scanner);
            return SqlWhereClause.Create(sqlScalarExpression);
        }

        private static SqlGroupByClause ParseGroupByClause(ref Scanner scanner)
        {
            ParseExpected(TokenKind.GroupKeyword, ref scanner);
            ParseExpected(TokenKind.ByKeyword, ref scanner);

            List<SqlScalarExpression> scalarExpressionList = ParseScalarExpressionList(ref scanner);
            return SqlGroupByClause.Create(scalarExpressionList);
        }

        private static SqlOrderByClause ParseOrderByClause(ref Scanner scanner)
        {
            ParseExpected(TokenKind.OrderKeyword, ref scanner);
            ParseExpected(TokenKind.ByKeyword, ref scanner);

            List<SqlOrderByItem> orderByItems = ParseOrderByItems(ref scanner);

            return SqlOrderByClause.Create(orderByItems);
        }

        private static List<SqlOrderByItem> ParseOrderByItems(ref Scanner scanner)
        {
            List<SqlOrderByItem> orderByItems = new List<SqlOrderByItem>();
            do
            {
                SqlOrderByItem orderByItem = ParseOrderByItem(ref scanner);
                orderByItems.Add(orderByItem);
            }
            while (scanner.Token == TokenKind.Comma);

            return orderByItems;
        }

        private static SqlOrderByItem ParseOrderByItem(ref Scanner scanner)
        {
            SqlScalarExpression expression = ParseScalarExpression(ref scanner);

            bool isDescending;
            if (scanner.Token == TokenKind.AscKeyword)
            {
                isDescending = false;
                scanner.Scan();
            }
            else if (scanner.Token == TokenKind.DescKeyword)
            {
                isDescending = true;
                scanner.Scan();
            }
            else
            {
                // By default it's asc.
                isDescending = false;
            }

            return SqlOrderByItem.Create(expression, isDescending);
        }

        private static SqlOffsetLimitClause ParseOffsetLimitClause(ref Scanner scanner)
        {
            ParseExpected(TokenKind.OffsetKeyword, ref scanner);
            Number64 offsetCount = ParseNumber(ref scanner);
            ParseExpected(TokenKind.LimitKeyword, ref scanner);
            Number64 limitCount = ParseNumber(ref scanner);

            SqlOffsetSpec offsetSpec = SqlOffsetSpec.Create(SqlNumberLiteral.Create(offsetCount));
            SqlLimitSpec limitSpec = SqlLimitSpec.Create(SqlNumberLiteral.Create(limitCount));

            return SqlOffsetLimitClause.Create(offsetSpec, limitSpec);
        }

        private static Number64 ParseNumber(ref Scanner scanner)
        {
            string numberAsString = scanner.TokenValue.ToString();
            Number64 number;
            if (long.TryParse(numberAsString, out long longValue))
            {
                number = longValue;
            }
            else
            {
                if (!double.TryParse(numberAsString, out double doubleValue))
                {
                    throw new ParseException($"The invalid double value: '{numberAsString}'");
                }

                number = doubleValue;
            }

            scanner.Scan();

            return number;
        }

        private static SqlScalarExpression ParseScalarExpression(ref Scanner scanner)
        {
            return ParseConditionalScalarExpression(ref scanner);
        }

        private static List<SqlScalarExpression> ParseScalarExpressionList(ref Scanner scanner)
        {
            List<SqlScalarExpression> expressions = new List<SqlScalarExpression>();

            expressions.Add(ParseScalarExpression(ref scanner));

            while (scanner.Token == TokenKind.Comma)
            {
                scanner.Scan();
                expressions.Add(ParseScalarExpression(ref scanner));
            }

            return expressions;
        }

        private static SqlScalarExpression ParseConditionalScalarExpression(ref Scanner scanner)
        {
            SqlScalarExpression expression = ParseBetweenOrInScalarExpression(ref scanner);

            if (scanner.Token == TokenKind.Question)
            {
                scanner.Scan();
                SqlScalarExpression consequent = ParseScalarExpression(ref scanner);
                ParseExpected(TokenKind.Colon, ref scanner);
                SqlScalarExpression alternative = ParseScalarExpression(ref scanner);
                return SqlConditionalScalarExpression.Create(
                    expression,
                    consequent,
                    alternative);
            }

            return expression;
        }

        private static SqlScalarExpression ParseBetweenOrInScalarExpression(ref Scanner scanner)
        {
            SqlScalarExpression expression = ParseBinaryScalarExpression(ref scanner);
            bool isIn = false;
            bool isBetween = false;

            Scanner lookAheadScanner = scanner;
            if (lookAheadScanner.Token == TokenKind.NotKeyword)
            {
                lookAheadScanner.Scan();
                isIn = lookAheadScanner.Token == TokenKind.InKeyword;
                isBetween = lookAheadScanner.Token == TokenKind.BetweenKeyword;
            }

            if (isIn)
            {
                return ParseInScalarExpression(expression, ref scanner);
            }

            if (isBetween)
            {
                return ParseBetweenScalarExpression(expression, ref scanner);
            }

            return expression;
        }

        private static SqlInScalarExpression ParseInScalarExpression(SqlScalarExpression scalarExpression, ref Scanner scanner)
        {
            bool inverted;
            if (scanner.Token == TokenKind.NotKeyword)
            {
                scanner.Scan();
                inverted = true;
            }
            else
            {
                inverted = false;
            }

            ParseExpected(TokenKind.InKeyword, ref scanner);
            ParseExpected(TokenKind.OpenParen, ref scanner);
            List<SqlScalarExpression> expressions = ParseScalarExpressionList(ref scanner);
            ParseExpected(TokenKind.CloseParen, ref scanner);

            return SqlInScalarExpression.Create(
                scalarExpression,
                inverted,
                expressions);
        }

        private static SqlBetweenScalarExpression ParseBetweenScalarExpression(SqlScalarExpression expression, ref Scanner scanner)
        {
            bool inverted;
            if (scanner.Token == TokenKind.NotKeyword)
            {
                scanner.Scan();
                inverted = true;
            }
            else
            {
                inverted = false;
            }

            ParseExpected(TokenKind.BetweenKeyword, ref scanner);
            SqlScalarExpression low = ParseBinaryScalarExpression(ref scanner);
            ParseExpected(TokenKind.AndKeyword, ref scanner);
            SqlScalarExpression high = ParseBinaryScalarExpression(ref scanner);

            return SqlBetweenScalarExpression.Create(expression, low, high, inverted);
        }

        private static SqlScalarExpression ParseBinaryScalarExpression(ref Scanner scanner, int precedence = 0)
        {
            SqlScalarExpression expression = ParseUnaryScalarExpression(ref scanner);

            while (true)
            {
                int operatorPrecedence = GetBinaryOperatorPrecedence(scanner.Token);
                if (operatorPrecedence < precedence)
                {
                    break;
                }

                TokenKind op = scanner.Token;
                scanner.Scan();

                SqlBinaryScalarOperatorKind operatorKind = TokenToBinaryOperatorKind(op);

                expression = SqlBinaryScalarExpression.Create(
                    operatorKind,
                    expression,
                    ParseBinaryScalarExpression(ref scanner, operatorPrecedence));
            }

            return expression;
        }

        private static SqlScalarExpression ParseUnaryScalarExpression(ref Scanner scanner)
        {
            TokenKind nextToken = scanner.Token;
            if (
                (nextToken == TokenKind.Plus)
                || (nextToken == TokenKind.Minus)
                || (nextToken == TokenKind.Tilde)
                || (nextToken == TokenKind.NotKeyword))
            {
                TokenKind op = scanner.Token;
                scanner.Scan();

                SqlUnaryScalarOperatorKind unaryOperator = TokenToUnaryOperatorKind(op);

                return SqlUnaryScalarExpression.Create(unaryOperator, ParseMemberExpression(ref scanner));
            }

            return ParseMemberExpression(ref scanner);
        }

        private static SqlScalarExpression ParseMemberExpression(ref Scanner scanner)
        {
            SqlScalarExpression expression = ParsePrimaryExpression(ref scanner);

            while (true)
            {
                if (scanner.Token == TokenKind.OpenBracket)
                {
                    scanner.Scan();
                    SqlScalarExpression value = ParseScalarExpression(ref scanner);
                    expression = SqlMemberIndexerScalarExpression.Create(member: value, indexer: expression);
                    ParseExpected(TokenKind.CloseBracket, ref scanner);
                }

                if (scanner.Token == TokenKind.Dot)
                {
                    scanner.Scan();
                    SqlIdentifier id = ParseIdentifier(ref scanner);

                    expression = SqlPropertyRefScalarExpression.Create(
                        member: expression,
                        identifier: id);
                }

                break;
            }

            return expression;
        }

        private static SqlScalarExpression ParsePrimaryExpression(ref Scanner scanner)
        {
            switch (scanner.Token)
            {
                case TokenKind.OpenBracket:
                    return ParseArrayCreateScalarExpression(ref scanner);
                case TokenKind.ArrayKeyword:
                    return ParseArrayScalarExpression(ref scanner);
                case TokenKind.StringLiteral:
                case TokenKind.NumericLiteral:
                case TokenKind.NullKeyword:
                case TokenKind.UndefinedKeyword:
                case TokenKind.TrueKeyword:
                case TokenKind.FalseKeyword:
                    return ParseLiteralScalarExpression(ref scanner);
                case TokenKind.ExistsKeyword:
                    return ParseExistsScalarExpression(ref scanner);
                case TokenKind.OpenBrace:
                    return ParseObjectCreateScalarExpression(ref scanner);
                case TokenKind.OpenParen:
                    return ParseSubqueryOrParenthesizedScalarExpression(ref scanner);
                default:
                    return ParseFunctionCallScalarExpression(ref scanner);
            }
        }

        private static SqlArrayCreateScalarExpression ParseArrayCreateScalarExpression(ref Scanner scanner)
        {
            ParseExpected(TokenKind.OpenBracket, ref scanner);

            if (scanner.Token == TokenKind.CloseBracket)
            {
                scanner.Scan();
                return SqlArrayCreateScalarExpression.Create();
            }

            List<SqlScalarExpression> expressions = ParseScalarExpressionList(ref scanner);

            ParseExpected(TokenKind.CloseBracket, ref scanner);

            return SqlArrayCreateScalarExpression.Create(expressions);
        }

        private static SqlArrayScalarExpression ParseArrayScalarExpression(ref Scanner scanner)
        {
            ParseExpected(TokenKind.ArrayKeyword, ref scanner);
            ParseExpected(TokenKind.OpenParen, ref scanner);

            SqlQuery sqlQuery = ParseQuery(ref scanner);

            return SqlArrayScalarExpression.Create(sqlQuery);
        }

        private static SqlScalarExpression ParseExistsScalarExpression(ref Scanner scanner)
        {
            if (scanner.Token == TokenKind.ExistsKeyword)
            {
                scanner.Scan();
                ParseExpected(TokenKind.OpenParen, ref scanner);
                SqlQuery sqlQuery = ParseQuery(ref scanner);
                ParseExpected(TokenKind.CloseParen, ref scanner);

                return SqlExistsScalarExpression.Create(sqlQuery);
            }

            return ParseConditionalScalarExpression(ref scanner);
        }

        private static SqlScalarExpression ParseSubqueryOrParenthesizedScalarExpression(ref Scanner scanner)
        {
            SqlScalarExpression expression;
            ParseExpected(TokenKind.OpenParen, ref scanner);
            if (scanner.Token == TokenKind.SelectKeyword)
            {
                SqlQuery subQuery = ParseQuery(ref scanner);
                expression = SqlSubqueryScalarExpression.Create(subQuery);
            }
            else
            {
                expression = ParseScalarExpression(ref scanner);
            }

            ParseExpected(TokenKind.CloseParen, ref scanner);
            return expression;
        }

        private static SqlFunctionCallScalarExpression ParseFunctionCallScalarExpression(ref Scanner scanner)
        {
            bool isUdf;
            if (scanner.Token == TokenKind.UdfKeyword)
            {
                isUdf = true;
                ParseExpected(TokenKind.UdfKeyword, ref scanner);
                ParseExpected(TokenKind.Dot, ref scanner);
            }
            else
            {
                isUdf = false;
            }

            SqlIdentifier identifier = ParseIdentifier(ref scanner);

            ParseExpected(TokenKind.OpenParen, ref scanner);
            List<SqlScalarExpression> arguments;
            if (scanner.Token == TokenKind.CloseParen)
            {
                arguments = new List<SqlScalarExpression>();
            }
            else
            {
                arguments = ParseScalarExpressionList(ref scanner);
            }

            ParseExpected(TokenKind.CloseParen, ref scanner);

            return SqlFunctionCallScalarExpression.Create(identifier, isUdf, arguments);
        }

        private static SqlObjectCreateScalarExpression ParseObjectCreateScalarExpression(ref Scanner scanner)
        {
            // Consume open brace
            scanner.Scan();

            List<SqlObjectProperty> properties = new List<SqlObjectProperty>();
            while (scanner.Token != TokenKind.CloseBrace)
            {
                // Slice off the quotes.
                string propertyName = scanner.TokenValue.Slice(start: 1, scanner.TokenValue.Length - 2).ToString();
                SqlPropertyName sqlPropertyName = SqlPropertyName.Create(propertyName);
                ParseExpected(TokenKind.Colon, ref scanner);

                SqlScalarExpression value = ParseScalarExpression(ref scanner);

                // Allow trailing commas
                if (scanner.Token == TokenKind.Comma)
                {
                    scanner.Scan();
                }

                properties.Add(SqlObjectProperty.Create(sqlPropertyName, value));
            }

            return SqlObjectCreateScalarExpression.Create(properties);
        }

        private static SqlLiteralScalarExpression ParseLiteralScalarExpression(ref Scanner scanner)
        {
            SqlLiteral sqlLiteral;
            if (scanner.Token == TokenKind.StringLiteral)
            {
                // Slice off the quotes.
                string stringValue = scanner.TokenValue.Slice(start: 1, scanner.TokenValue.Length - 2).ToString();
                sqlLiteral = SqlStringLiteral.Create(stringValue);
            }
            else if (scanner.Token == TokenKind.NumericLiteral)
            {
                string numberToString = scanner.TokenValue.ToString();
                Number64 number;
                if (long.TryParse(numberToString, out long longValue))
                {
                    number = longValue;
                }
                else
                {
                    if (!double.TryParse(numberToString, out double doubleValue))
                    {
                        throw new ParseException($"The invalid double value: '{scanner.TokenValue.ToString()}'");
                    }

                    number = doubleValue;
                }

                sqlLiteral = SqlNumberLiteral.Create(number);
            }
            else if (scanner.Token == TokenKind.NullKeyword)
            {
                sqlLiteral = SqlNullLiteral.Singleton;
            }
            else if (scanner.Token == TokenKind.TrueKeyword)
            {
                sqlLiteral = SqlBooleanLiteral.True;
            }
            else if (scanner.Token == TokenKind.FalseKeyword)
            {
                sqlLiteral = SqlBooleanLiteral.False;
            }
            else if (scanner.Token == TokenKind.UndefinedKeyword)
            {
                sqlLiteral = SqlUndefinedLiteral.Create();
            }
            else
            {
                throw new ParseException($"Unexpected token kind for SqlLiteral : {scanner.Token}.");
            }

            scanner.Scan();

            return SqlLiteralScalarExpression.Create(sqlLiteral);
        }

        private static SqlBinaryScalarOperatorKind TokenToBinaryOperatorKind(TokenKind tokenKind) => tokenKind switch
        {
            TokenKind.Star => SqlBinaryScalarOperatorKind.Multiply,
            TokenKind.Slash => SqlBinaryScalarOperatorKind.Divide,
            TokenKind.Percent => SqlBinaryScalarOperatorKind.Modulo,
            TokenKind.Plus => SqlBinaryScalarOperatorKind.Add,
            TokenKind.Minus => SqlBinaryScalarOperatorKind.Subtract,
            TokenKind.GreaterThan => SqlBinaryScalarOperatorKind.GreaterThan,
            TokenKind.GreaterThanOrEquals => SqlBinaryScalarOperatorKind.GreaterThanOrEqual,
            TokenKind.LessThan => SqlBinaryScalarOperatorKind.LessThan,
            TokenKind.LessThanOrEquals => SqlBinaryScalarOperatorKind.LessThanOrEqual,
            TokenKind.Equals => SqlBinaryScalarOperatorKind.Equal,
            TokenKind.NotEquals => SqlBinaryScalarOperatorKind.NotEqual,
            TokenKind.Ampersand => SqlBinaryScalarOperatorKind.BitwiseAnd,
            TokenKind.Pipe => SqlBinaryScalarOperatorKind.BitwiseOr,
            TokenKind.AndKeyword => SqlBinaryScalarOperatorKind.And,
            TokenKind.OrKeyword => SqlBinaryScalarOperatorKind.Or,
            TokenKind.DoubleQuestion => SqlBinaryScalarOperatorKind.Coalesce,
            TokenKind.DoublePipe => SqlBinaryScalarOperatorKind.StringConcat,
            _ => throw new ParseException($"Unexpected token for binary operator: {tokenKind}."),
        };

        private static SqlUnaryScalarOperatorKind TokenToUnaryOperatorKind(TokenKind tokenKind) => tokenKind switch
        {
            TokenKind.Plus => SqlUnaryScalarOperatorKind.Plus,
            TokenKind.Minus => SqlUnaryScalarOperatorKind.Minus,
            TokenKind.Tilde => SqlUnaryScalarOperatorKind.BitwiseNot,
            TokenKind.NotKeyword => SqlUnaryScalarOperatorKind.Not,
            _ => throw new ParseException($"Unexpected token for unary operator: {tokenKind}."),
        };

        private static int GetBinaryOperatorPrecedence(TokenKind tokenKind)
        {
            switch (tokenKind)
            {
                case TokenKind.Star:
                case TokenKind.Slash:
                case TokenKind.Percent:
                    return 100;
                case TokenKind.Plus:
                case TokenKind.Minus:
                    return 90;
                case TokenKind.GreaterThan:
                case TokenKind.GreaterThanOrEquals:
                case TokenKind.LessThan:
                case TokenKind.LessThanOrEquals:
                    return 80;
                case TokenKind.Equals:
                case TokenKind.NotEquals:
                    return 70;
                case TokenKind.Ampersand:
                case TokenKind.Pipe:
                    return 60;
                case TokenKind.AndKeyword:
                case TokenKind.OrKeyword:
                case TokenKind.DoubleQuestion:
                    return 50;
            }

            return -1;
        }

        private static SqlIdentifier ParseIdentifier(ref Scanner scanner)
        {
            if (scanner.Token == TokenKind.Identifier)
            {
                throw new ParseException($"Expected identifier token, but instead got: {scanner.Token}: {scanner.TokenValue.ToString()}.");
            }

            SqlIdentifier sqlIdentifier = SqlIdentifier.Create(scanner.TokenValue.ToString());

            scanner.Scan();

            return sqlIdentifier;
        }

        private static void ParseExpected(TokenKind tokenKind, ref Scanner scanner)
        {
            if (scanner.Token != tokenKind)
            {
                throw new ParseException($"Parse error: expected {tokenKind}");
            }

            scanner.Scan();
        }

        private sealed class ParseException : Exception
        {
            public ParseException(string message)
                : base(message)
            {
            }
        }
    }
}
