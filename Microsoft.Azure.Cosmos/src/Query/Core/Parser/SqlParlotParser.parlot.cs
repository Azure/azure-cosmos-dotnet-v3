// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

// Build-only grammar: the Parlot source generator runs this factory at compile time and emits the parser into
// this assembly. This file is not compiled into the SDK and no Parlot assembly is referenced at runtime.
namespace Microsoft.Azure.Cosmos.Query.Core.Parser
{
    using System;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Parlot.Fluent;
    using Parlot.SourceGenerator;
    using static Parlot.Fluent.Parsers;

    internal static partial class SqlParlotParser
    {
        // The parser reads chains of prefix operators in a loop, but the code that visits the tree recurses once per
        // level. The limit keeps the tree shallow enough for it on a 1 MB stack: about 300 nested parentheses, 190
        // subqueries or 1,000 prefix operators.
        [GenerateParser(nameof(TryParse), MaxRecursionDepth = 1000)]
        private static Parser<SqlQuery> Build()
        {
            // Keywords are matched as whole words, as the ANTLR lexer reads the longest token.
            static Parser<string> Keyword(string text) => Terms.Keyword(text, IdentifierPartChars, caseInsensitive: true);
            static Parser<string> CaseSensitiveKeyword(string text) => Terms.Keyword(text, IdentifierPartChars);

            Parser<string> kAll = Keyword("all");
            Parser<string> kAnd = Keyword("and");
            Parser<string> kArray = Keyword("array");
            Parser<string> kAs = Keyword("as");
            Parser<string> kAsc = Keyword("asc");
            Parser<string> kBetween = Keyword("between");
            Parser<string> kBy = Keyword("by");
            Parser<string> kDesc = Keyword("desc");
            Parser<string> kDistinct = Keyword("distinct");
            Parser<string> kEscape = Keyword("escape");
            Parser<string> kExists = Keyword("exists");
            Parser<string> kFirst = Keyword("first");
            Parser<string> kFrom = Keyword("from");
            Parser<string> kGroup = Keyword("group");
            Parser<string> kIn = Keyword("in");
            Parser<string> kJoin = Keyword("join");
            Parser<string> kLast = Keyword("last");
            Parser<string> kLeft = Terms.Keyword("left", IdentifierPartChars, caseInsensitive: true, returnMatchedText: true);
            Parser<string> kLike = Keyword("like");
            Parser<string> kLimit = Keyword("limit");
            Parser<string> kNot = Keyword("not");
            Parser<string> kOffset = Keyword("offset");
            Parser<string> kOr = Keyword("or");
            Parser<string> kOrder = Keyword("order");
            Parser<string> kRank = Keyword("rank");
            Parser<string> kRight = Terms.Keyword("right", IdentifierPartChars, caseInsensitive: true, returnMatchedText: true);
            Parser<string> kSelect = Keyword("select");
            Parser<string> kTop = Keyword("top");
            Parser<string> kValue = Keyword("value");
            Parser<string> kWhere = Keyword("where");
            Parser<string> kFalse = CaseSensitiveKeyword("false");
            Parser<string> kNull = CaseSensitiveKeyword("null");
            Parser<string> kTrue = CaseSensitiveKeyword("true");
            Parser<string> kUdf = CaseSensitiveKeyword("udf");
            Parser<string> kUndefined = CaseSensitiveKeyword("undefined");

            Parser<char> lParen = Terms.Char('(');
            Parser<char> rParen = Terms.Char(')');
            Parser<char> lBracket = Terms.Char('[');
            Parser<char> rBracket = Terms.Char(']');
            Parser<char> lBrace = Terms.Char('{');
            Parser<char> rBrace = Terms.Char('}');
            Parser<char> comma = Terms.Char(',');
            Parser<char> dot = Terms.Char('.');
            Parser<char> colon = Terms.Char(':');

            Parser<SqlIdentifier> identifier = Terms.Scan(ScanIdentifier, IdentifierStartChars.AsSpan())
                .Then(static span => SqlIdentifier.Create(span.ToString()));
            Parser<SqlParameter> parameter = Terms.Scan(ScanParameter, "@".AsSpan())
                .Then(static span => SqlParameter.Create(span.ToString()));
            Parser<SqlNumberLiteral> number = Terms.Scan(ScanNumber, NumberStartChars.AsSpan())
                .Then(static span => SqlParlotParser.CreateNumberLiteral(span.Span));
            Parser<SqlStringLiteral> stringLiteral = Terms.Scan(ScanString, "\"'".AsSpan())
                .Then(static span => SqlStringLiteral.Create(SqlParlotParser.GetStringValue(span.Span)));

            Deferred<SqlQuery> sqlQuery = Deferred<SqlQuery>();
            Deferred<SqlScalarExpression> scalarExpression = Deferred<SqlScalarExpression>();

            Parser<SqlQuery> parenthesizedQuery = Between(lParen, sqlQuery, rParen);
            Parser<System.Collections.Generic.IReadOnlyList<SqlScalarExpression>> scalarExpressionList = Separated(comma, scalarExpression);
            Parser<System.Collections.Generic.IReadOnlyList<SqlScalarExpression>> arguments = Between(lParen, Separated(comma, scalarExpression, min: 0), rParen);

            // function_call_scalar_expression
            Parser<SqlFunctionCallScalarExpression> functionCall = OneOf(
                kUdf.SkipAnd(dot).SkipAnd(identifier).And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(t.Item1, true, SqlParlotParser.ToImmutableArray(t.Item2))),
                kLeft.And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(SqlIdentifier.Create(t.Item1), false, SqlParlotParser.ToImmutableArray(t.Item2))),
                kRight.And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(SqlIdentifier.Create(t.Item1), false, SqlParlotParser.ToImmutableArray(t.Item2))),
                identifier.And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(t.Item1, false, SqlParlotParser.ToImmutableArray(t.Item2))));

            Parser<SqlObjectProperty> objectProperty = stringLiteral.AndSkip(colon).And(scalarExpression)
                .Then(static t => SqlObjectProperty.Create(SqlPropertyName.Create(t.Item1.Value), t.Item2));

            // primary_expression, without the left recursive alternatives
            Parser<SqlScalarExpression> primaryBase = OneOf(
                number.Then(static n => (SqlScalarExpression)SqlLiteralScalarExpression.Create(n)),
                stringLiteral.Then(static s => (SqlScalarExpression)SqlLiteralScalarExpression.Create(s)),
                parameter.Then(static p => (SqlScalarExpression)SqlParameterRefScalarExpression.Create(p)),
                kTrue.Then(static _ => (SqlScalarExpression)SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(true))),
                kFalse.Then(static _ => (SqlScalarExpression)SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(false))),
                kNull.Then(static _ => (SqlScalarExpression)SqlLiteralScalarExpression.Create(SqlNullLiteral.Create())),
                kUndefined.Then(static _ => (SqlScalarExpression)SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create())),
                kExists.SkipAnd(parenthesizedQuery).Then(static q => (SqlScalarExpression)SqlExistsScalarExpression.Create(q)),
                kArray.SkipAnd(parenthesizedQuery).Then(static q => (SqlScalarExpression)SqlArrayScalarExpression.Create(q)),
                kAll.SkipAnd(parenthesizedQuery).Then(static q => (SqlScalarExpression)SqlAllScalarExpression.Create(q)),
                kFirst.SkipAnd(parenthesizedQuery).Then(static q => (SqlScalarExpression)SqlFirstScalarExpression.Create(q)),
                kLast.SkipAnd(parenthesizedQuery).Then(static q => (SqlScalarExpression)SqlLastScalarExpression.Create(q)),
                kUdf.SkipAnd(dot).SkipAnd(identifier).And(arguments)
                    .Then(static t => (SqlScalarExpression)SqlFunctionCallScalarExpression.Create(t.Item1, true, SqlParlotParser.ToImmutableArray(t.Item2))),
                kLeft.And(arguments)
                    .Then(static t => (SqlScalarExpression)SqlFunctionCallScalarExpression.Create(SqlIdentifier.Create(t.Item1), false, SqlParlotParser.ToImmutableArray(t.Item2))),
                kRight.And(arguments)
                    .Then(static t => (SqlScalarExpression)SqlFunctionCallScalarExpression.Create(SqlIdentifier.Create(t.Item1), false, SqlParlotParser.ToImmutableArray(t.Item2))),
                identifier.And(ZeroOrOne(arguments))
                    .Then(static t => t.Item2.Count == 0
                        ? (SqlScalarExpression)SqlPropertyRefScalarExpression.Create(null, t.Item1)
                        : SqlFunctionCallScalarExpression.Create(t.Item1, false, SqlParlotParser.ToImmutableArray(t.Item2[0]))),
                Between(lBracket, Separated(comma, scalarExpression, min: 0), rBracket)
                    .Then(static items => (SqlScalarExpression)SqlArrayCreateScalarExpression.Create(SqlParlotParser.ToImmutableArray(items))),
                Between(lBrace, Separated(comma, objectProperty, min: 0), rBrace)
                    .Then(static properties => (SqlScalarExpression)SqlObjectCreateScalarExpression.Create(SqlParlotParser.ToImmutableArray(properties))),
                parenthesizedQuery.Then(static q => (SqlScalarExpression)SqlSubqueryScalarExpression.Create(q)),
                Between(lParen, scalarExpression, rParen));

            // primary_expression, then unary_scalar_expression and binary_scalar_expression, from the level that binds
            // the tightest.
            Parser<SqlScalarExpression> binaryExpression = primaryBase.Operators()
                .Postfix(
                    (dot.SkipAnd(identifier), static (e, id) => SqlPropertyRefScalarExpression.Create(e, id)),
                    (Between(lBracket, scalarExpression, rBracket), static (e, index) => SqlMemberIndexerScalarExpression.Create(e, index)))
                .Unary(
                    (Terms.Scan(ScanMinus, "-".AsSpan()), static e => SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Minus, e)),
                    (Terms.Scan(ScanPlus, "+".AsSpan()), static e => SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Plus, e)),
                    (Terms.Char('~').Then(static _ => default(Parlot.TextSpan)), static e => SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.BitwiseNot, e)),
                    (kNot.Then(static _ => default(Parlot.TextSpan)), static e => SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Not, e)))
                .LeftAssociative(
                    (Terms.Char('*'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Multiply, a, b)),
                    (Terms.Char('/'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Divide, a, b)),
                    (Terms.Char('%'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Modulo, a, b)))
                .LeftAssociative(
                    (Terms.Scan(ScanPlus, "+".AsSpan()), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Add, a, b)),
                    (Terms.Scan(ScanMinus, "-".AsSpan()), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Subtract, a, b)))
                .LeftAssociative(
                    (Terms.Text(">="), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThanOrEqual, a, b)),
                    (Terms.Text("<="), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThanOrEqual, a, b)),
                    (Terms.Text("<"), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThan, a, b)),
                    (Terms.Text(">"), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThan, a, b)))
                .LeftAssociative(
                    (Terms.Text("="), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Equal, a, b)),
                    (Terms.Text("!="), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.NotEqual, a, b)))
                .LeftAssociative((Terms.Char('&'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseAnd, a, b)))
                .LeftAssociative((Terms.Char('^'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseXor, a, b)))
                .LeftAssociative((Terms.Char('|').WhenNotFollowedBy(Literals.Char('|')), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseOr, a, b)))
                .LeftAssociative((Terms.Text("||"), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.StringConcat, a, b)));

            // in_scalar_expression, like_scalar_expression and the BETWEEN alternative of scalar_expression,
            // after their binary_scalar_expression.
            Parser<Suffix> inSuffix = kIn.SkipAnd(Between(lParen, scalarExpressionList, rParen))
                .Then(static list => new Suffix(SuffixKind.In, SqlParlotParser.ToImmutableArray(list), null, null, null));
            Parser<Suffix> likeSuffix = kLike.SkipAnd(binaryExpression).And(ZeroOrOne(kEscape.SkipAnd(stringLiteral)))
                .Then(static t => new Suffix(SuffixKind.Like, default, t.Item1, null, t.Item2.Count == 0 ? null : t.Item2[0]));
            Parser<Suffix> betweenSuffix = kBetween.SkipAnd(binaryExpression).AndSkip(kAnd).And(binaryExpression)
                .Then(static t => new Suffix(SuffixKind.Between, default, t.Item1, t.Item2, null));

            Parser<Suffix> logicalSuffix = ZeroOrOne(kNot).And(OneOf(inSuffix, likeSuffix))
                .Then(static t => t.Item2.WithNot(t.Item1.Count != 0));
            Parser<Suffix> firstSuffix = ZeroOrOne(kNot).And(OneOf(inSuffix, likeSuffix, betweenSuffix))
                .Then(static t => t.Item2.WithNot(t.Item1.Count != 0));

            Parser<SqlScalarExpression> logicalOperand = binaryExpression.And(ZeroOrOne(logicalSuffix))
                .Then(static t => SqlParlotParser.CreateLeaf(t.Item1, t.Item2).Expression);
            Parser<SqlScalarExpression> andExpression = logicalOperand.LeftAssociative(
                (kAnd, static (left, right) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, left, right)));

            // logical_scalar_expression | BETWEEN, the primary alternatives of scalar_expression.
            Parser<SqlScalarExpression> scalarPrimary = binaryExpression.And(ZeroOrOne(firstSuffix))
                .Then(static t => SqlParlotParser.CreateLeaf(t.Item1, t.Item2))
                .And(ZeroOrMany(kAnd.SkipAnd(logicalOperand)))
                .And(ZeroOrMany(kOr.SkipAnd(andExpression)))
                .When(static (c, t) => SqlParlotParser.IsValidLogical(t.Item1, t.Item2, t.Item3))
                .Then(static t => SqlParlotParser.CreateLogical(t.Item1, t.Item2, t.Item3));

            // scalar_expression '?' scalar_expression ':' scalar_expression and scalar_expression '??' scalar_expression.
            // The alternative of a conditional can't contain a conditional or a coalesce expression, which makes
            // conditionals left associative, and the right operand of a coalesce expression can't contain one.
            scalarExpression.Parser = scalarPrimary.Operators()
                .Ternary(
                    Terms.Char('?').WhenNotFollowedBy(Literals.Char('?')),
                    scalarExpression,
                    colon,
                    static (condition, consequent, alternative) => SqlConditionalScalarExpression.Create(condition, consequent, alternative),
                    rightAssociative: false)
                .LeftAssociative((Terms.Text("??"), static (left, right) => SqlCoalesceScalarExpression.Create(left, right)));

            // SELECT
            Parser<SqlTopSpec> topSpec = kTop.SkipAnd(OneOf(
                number.Then(static n => SqlTopSpec.Create(n)),
                parameter.Then(static p => SqlTopSpec.Create(p))));
            Parser<SqlSelectItem> selectItem = scalarExpression.And(ZeroOrOne(kAs.SkipAnd(identifier)))
                .Then(static t => SqlSelectItem.Create(t.Item1, t.Item2.Count == 0 ? null : t.Item2[0]));
            Parser<SqlSelectSpec> selection = OneOf(
                Terms.Char('*').Then(static _ => (SqlSelectSpec)SqlSelectStarSpec.Create()),
                kValue.SkipAnd(scalarExpression).Then(static e => (SqlSelectSpec)SqlSelectValueSpec.Create(e)),
                Separated(comma, selectItem).Then(static items => (SqlSelectSpec)SqlSelectListSpec.Create(SqlParlotParser.ToImmutableArray(items))));
            Parser<SqlSelectClause> selectClause = kSelect.SkipAnd(ZeroOrOne(kDistinct)).And(ZeroOrOne(topSpec)).And(selection)
                .Then(static t => SqlSelectClause.Create(t.Item3, t.Item2.Count == 0 ? null : t.Item2[0], t.Item1.Count != 0));

            // FROM
            Parser<PathSegment> pathSegment = OneOf(
                dot.SkipAnd(identifier).Then(static id => new PathSegment(id, null, null)),
                Between(lBracket, number, rBracket).Then(static n => new PathSegment(null, n, null)),
                Between(lBracket, stringLiteral, rBracket).Then(static s => new PathSegment(null, null, s)));
            Parser<SqlCollection> collection = OneOf(
                identifier.And(ZeroOrMany(pathSegment))
                    .Then(static t => (SqlCollection)SqlInputPathCollection.Create(t.Item1, SqlParlotParser.CreatePath(t.Item2))),
                parenthesizedQuery.Then(static q => (SqlCollection)SqlSubqueryCollection.Create(q)));
            Parser<SqlCollectionExpression> collectionPrimary = OneOf(
                identifier.AndSkip(kIn).And(collection)
                    .Then(static t => (SqlCollectionExpression)SqlArrayIteratorCollectionExpression.Create(t.Item1, t.Item2)),
                collection.And(ZeroOrOne(ZeroOrOne(kAs).SkipAnd(identifier)))
                    .Then(static t => (SqlCollectionExpression)SqlAliasedCollectionExpression.Create(t.Item1, t.Item2.Count == 0 ? null : t.Item2[0])));
            Parser<SqlCollectionExpression> collectionExpression = collectionPrimary.LeftAssociative(
                (kJoin, static (left, right) => (SqlCollectionExpression)SqlJoinCollectionExpression.Create(left, right)));
            Parser<SqlFromClause> fromClause = kFrom.SkipAnd(collectionExpression).Then(static c => SqlFromClause.Create(c));

            // WHERE, GROUP BY
            Parser<SqlWhereClause> whereClause = kWhere.SkipAnd(scalarExpression).Then(static e => SqlWhereClause.Create(e));
            Parser<SqlGroupByClause> groupByClause = kGroup.SkipAnd(kBy).SkipAnd(scalarExpressionList)
                .Then(static items => SqlGroupByClause.Create(SqlParlotParser.ToImmutableArray(items)));

            // ORDER BY
            Parser<bool> sortOrder = OneOf(kAsc.Then(static _ => false), kDesc.Then(static _ => true));
            Parser<SqlOrderByItem> orderByItem = scalarExpression.And(ZeroOrOne(sortOrder))
                .Then(static t => SqlOrderByItem.Create(t.Item1, t.Item2.Count != 0 && t.Item2[0]));
            Parser<SqlOrderByItem> scoreOrderByItem = functionCall.And(ZeroOrOne(sortOrder))
                .Then(static t => SqlOrderByItem.Create(t.Item1, t.Item2.Count == 0 ? null : t.Item2[0]));
            Parser<SqlOrderByClause> orderByClause = kOrder.SkipAnd(kBy).SkipAnd(OneOf(
                kRank.SkipAnd(Separated(comma, scoreOrderByItem))
                    .Then(static items => SqlOrderByClause.Create(true, SqlParlotParser.ToImmutableArray(items))),
                Separated(comma, orderByItem)
                    .Then(static items => SqlOrderByClause.Create(false, SqlParlotParser.ToImmutableArray(items)))));

            // OFFSET LIMIT
            Parser<SqlOffsetSpec> offsetCount = OneOf(
                number.Then(static n => SqlOffsetSpec.Create(n)),
                parameter.Then(static p => SqlOffsetSpec.Create(p)));
            Parser<SqlLimitSpec> limitCount = OneOf(
                number.Then(static n => SqlLimitSpec.Create(n)),
                parameter.Then(static p => SqlLimitSpec.Create(p)));
            Parser<SqlOffsetLimitClause> offsetLimitClause = kOffset.SkipAnd(offsetCount).AndSkip(kLimit).And(limitCount)
                .Then(static t => SqlOffsetLimitClause.Create(t.Item1, t.Item2));

            sqlQuery.Parser = selectClause
                .And(ZeroOrOne(fromClause))
                .And(ZeroOrOne(whereClause))
                .And(ZeroOrOne(groupByClause))
                .And(ZeroOrOne(orderByClause))
                .And(ZeroOrOne(offsetLimitClause))
                .Then(static t => SqlQuery.Create(
                    t.Item1,
                    t.Item2.Count == 0 ? null : t.Item2[0],
                    t.Item3.Count == 0 ? null : t.Item3[0],
                    t.Item4.Count == 0 ? null : t.Item4[0],
                    t.Item5.Count == 0 ? null : t.Item5[0],
                    t.Item6.Count == 0 ? null : t.Item6[0]));

            // The white space is space, tab, carriage return and line feed.
            return sqlQuery
                .AndSkip(ZeroOrOne(Literals.WhiteSpace(includeNewLines: true)))
                .Eof()
                .WithWhiteSpaceSet(Parlot.WhiteSpaceSet.SpaceTabCrLf);
        }
    }
}
