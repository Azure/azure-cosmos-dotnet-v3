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
        // The limit bounds what the parser recurses on and the prefix operators, which it reads in a loop, to about 300
        // nested parentheses, 190 subqueries or 1,000 prefix operators. The binary operators and member accesses of a
        // chain aren't counted: a tree can be deeper than this, and the code that visits one has to check the stack.
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

            // LEX_IDENTIFIER, and PARAMETER : '@' LEX_IDENTIFIER, where LEX_IDENTIFIER can be empty.
            Parser<Parlot.TextSpan> word = Capture(Literals.AnyOf(IdentifierStartChars, minSize: 1, maxSize: 1).And(Literals.AnyOf(IdentifierPartChars).Optional()));
            // Not a word followed by a check, which would report a misplaced keyword at its end rather than at its start.
            Parser<SqlIdentifier> identifier = Terms.Scan(ScanIdentifier, IdentifierStartChars.AsSpan())
                .Then(static span => SqlIdentifier.Create(span.ToString()));
            Parser<SqlParameter> parameter = SkipWhiteSpace(Capture(Literals.Char('@').And(word.Optional())))
                .Then(static span => SqlParameter.Create(span.ToString()));

            // NUMERIC_LITERAL : ('+' | '-')? DIGIT+ ('.' DIGIT*)? (E [-+]? DIGIT+)? | ('+' | '-')? '.' DIGIT+ (E [-+]? DIGIT+)?
            Parser<Parlot.TextSpan> digits = Literals.AnyOf("0123456789");
            Parser<Parlot.TextSpan> sign = Literals.AnyOf("+-", minSize: 1, maxSize: 1);
            Parser<Parlot.TextSpan> unsignedNumber = Capture(
                OneOf(
                    Capture(digits.And(Literals.Char('.').And(digits.Optional()).Optional())),
                    Capture(Literals.Char('.').And(digits)))
                .And(Literals.AnyOf("eE", minSize: 1, maxSize: 1).And(sign.Optional()).And(digits).Optional()));
            Parser<SqlNumberLiteral> number = SkipWhiteSpace(OneOf(Capture(sign.And(unsignedNumber)), unsignedNumber))
                .Then(static span => SqlParlotParser.CreateNumberLiteral(span.Span));

            // The '-' and '+' operators: the lexer reads a sign followed by a number as a numeric literal.
            Parser<char> minus = Terms.Char('-').WhenNotFollowedBy(unsignedNumber);
            Parser<char> plus = Terms.Char('+').WhenNotFollowedBy(unsignedNumber);

            Parser<SqlStringLiteral> stringLiteral = Terms.Scan(ScanString, "\"'".AsSpan())
                .Then(static span => SqlStringLiteral.Create(SqlParlotParser.GetStringValue(span.Span)));

            Deferred<SqlQuery> sqlQuery = Deferred<SqlQuery>();
            Deferred<SqlScalarExpression> scalarExpression = Deferred<SqlScalarExpression>();

            Parser<SqlQuery> parenthesizedQuery = Between(lParen, sqlQuery, rParen);
            Parser<System.Collections.Generic.IReadOnlyList<SqlScalarExpression>> scalarExpressionList = Separated(comma, scalarExpression);
            Parser<System.Collections.Generic.IReadOnlyList<SqlScalarExpression>> arguments = Between(lParen, Separated(comma, scalarExpression, min: 0), rParen);

            // function_call_scalar_expression
            Parser<SqlFunctionCallScalarExpression> keywordCall = OneOf(
                kUdf.SkipAnd(dot).SkipAnd(identifier).And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(t.Item1, true, SqlParlotParser.ToImmutableArray(t.Item2))),
                OneOf(kLeft, kRight).And(arguments)
                    .Then(static t => SqlFunctionCallScalarExpression.Create(SqlIdentifier.Create(t.Item1), false, SqlParlotParser.ToImmutableArray(t.Item2))));
            Parser<SqlFunctionCallScalarExpression> functionCall = OneOf(
                keywordCall,
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
                keywordCall.Then(static call => (SqlScalarExpression)call),
                identifier.And(arguments.Optional())
                    .Then(static t => t.Item2.HasValue
                        ? SqlFunctionCallScalarExpression.Create(t.Item1, false, SqlParlotParser.ToImmutableArray(t.Item2.Value))
                        : (SqlScalarExpression)SqlPropertyRefScalarExpression.Create(null, t.Item1)),
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
                .Unary((
                    OneOf(
                        minus.Then(SqlUnaryScalarOperatorKind.Minus),
                        plus.Then(SqlUnaryScalarOperatorKind.Plus),
                        Terms.Char('~').Then(SqlUnaryScalarOperatorKind.BitwiseNot),
                        kNot.Then(SqlUnaryScalarOperatorKind.Not)),
                    static (e, kind) => SqlUnaryScalarExpression.Create(kind, e)))
                .LeftAssociative(
                    (Terms.Char('*'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Multiply, a, b)),
                    (Terms.Char('/'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Divide, a, b)),
                    (Terms.Char('%'), static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Modulo, a, b)))
                .LeftAssociative(
                    (plus, static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Add, a, b)),
                    (minus, static (a, b) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Subtract, a, b)))
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
            Parser<Suffix> likeSuffix = kLike.SkipAnd(binaryExpression).And(kEscape.SkipAnd(stringLiteral).Optional())
                .Then(static t => new Suffix(SuffixKind.Like, default, t.Item1, null, t.Item2.OrSome(null)));
            Parser<Suffix> betweenSuffix = kBetween.SkipAnd(binaryExpression).AndSkip(kAnd).And(binaryExpression)
                .Then(static t => new Suffix(SuffixKind.Between, default, t.Item1, t.Item2, null));

            Parser<Suffix> logicalSuffix = kNot.Optional().And(OneOf(inSuffix, likeSuffix))
                .Then(static t => t.Item2.WithNot(t.Item1.HasValue));
            Parser<Suffix> firstSuffix = kNot.Optional().And(OneOf(inSuffix, likeSuffix, betweenSuffix))
                .Then(static t => t.Item2.WithNot(t.Item1.HasValue));

            Parser<SqlScalarExpression> logicalOperand = binaryExpression.And(logicalSuffix.Optional())
                .Then(static t => t.Item2.HasValue ? SqlParlotParser.CreateLeaf(t.Item1, t.Item2.Value).Expression : t.Item1);
            Parser<SqlScalarExpression> andExpression = logicalOperand.LeftAssociative(
                (kAnd, static (left, right) => SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, left, right)));

            // logical_scalar_expression | BETWEEN, the primary alternatives of scalar_expression.
            Parser<SqlScalarExpression> scalarPrimary = binaryExpression.And(firstSuffix.Optional())
                .Then(static t => t.Item2.HasValue ? SqlParlotParser.CreateLeaf(t.Item1, t.Item2.Value) : new Leaf(t.Item1, isBetween: false))
                .And(ZeroOrMany(kAnd.SkipAnd(logicalOperand)))
                .And(ZeroOrMany(kOr.SkipAnd(andExpression)))
                .ThenTry(static t => SqlParlotParser.TryCreateLogical(t.Item1, t.Item2, t.Item3));

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
            Parser<SqlSelectItem> selectItem = scalarExpression.And(kAs.SkipAnd(identifier).Optional())
                .Then(static t => SqlSelectItem.Create(t.Item1, t.Item2.OrSome(null)));
            Parser<SqlSelectSpec> selection = OneOf(
                Terms.Char('*').Then(static _ => (SqlSelectSpec)SqlSelectStarSpec.Create()),
                kValue.SkipAnd(scalarExpression).Then(static e => (SqlSelectSpec)SqlSelectValueSpec.Create(e)),
                Separated(comma, selectItem).Then(static items => (SqlSelectSpec)SqlSelectListSpec.Create(SqlParlotParser.ToImmutableArray(items))));
            Parser<SqlSelectClause> selectClause = kSelect.SkipAnd(kDistinct.Optional()).And(topSpec.Optional()).And(selection)
                .Then(static t => SqlSelectClause.Create(t.Item3, t.Item2.OrSome(null), t.Item1.HasValue));

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
                collection.And(kAs.Optional().SkipAnd(identifier).Optional())
                    .Then(static t => (SqlCollectionExpression)SqlAliasedCollectionExpression.Create(t.Item1, t.Item2.OrSome(null))));
            Parser<SqlCollectionExpression> collectionExpression = collectionPrimary.LeftAssociative(
                (kJoin, static (left, right) => (SqlCollectionExpression)SqlJoinCollectionExpression.Create(left, right)));
            Parser<SqlFromClause> fromClause = kFrom.SkipAnd(collectionExpression).Then(static c => SqlFromClause.Create(c));

            // WHERE, GROUP BY
            Parser<SqlWhereClause> whereClause = kWhere.SkipAnd(scalarExpression).Then(static e => SqlWhereClause.Create(e));
            Parser<SqlGroupByClause> groupByClause = kGroup.SkipAnd(kBy).SkipAnd(scalarExpressionList)
                .Then(static items => SqlGroupByClause.Create(SqlParlotParser.ToImmutableArray(items)));

            // ORDER BY
            Parser<bool> sortOrder = OneOf(kAsc.Then(static _ => false), kDesc.Then(static _ => true));
            Parser<SqlOrderByItem> orderByItem = scalarExpression.And(sortOrder.Optional())
                .Then(static t => SqlOrderByItem.Create(t.Item1, t.Item2.OrSome(false)));
            Parser<SqlOrderByItem> scoreOrderByItem = functionCall.And(sortOrder.Optional())
                .Then(static t => SqlOrderByItem.Create(t.Item1, t.Item2.HasValue ? t.Item2.Value : null));
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
                .And(fromClause.Optional())
                .And(whereClause.Optional())
                .And(groupByClause.Optional())
                .And(orderByClause.Optional())
                .And(offsetLimitClause.Optional())
                .Then(static t => SqlQuery.Create(
                    t.Item1,
                    t.Item2.OrSome(null),
                    t.Item3.OrSome(null),
                    t.Item4.OrSome(null),
                    t.Item5.OrSome(null),
                    t.Item6.OrSome(null)));

            // The white space is space, tab, carriage return and line feed.
            return sqlQuery
                .AndSkip(Literals.WhiteSpace(includeNewLines: true).Optional())
                .Eof()
                .WithWhiteSpaceSet(Parlot.WhiteSpaceSet.SpaceTabCrLf);
        }
    }
}
