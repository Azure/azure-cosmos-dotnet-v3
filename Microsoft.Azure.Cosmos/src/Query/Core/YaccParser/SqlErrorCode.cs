// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query.Core.YaccParser
{
    internal enum SqlErrorCode
    {
        None = 0,

        // Parser Errors: 1000 -> 1999
        IncorrectSyntax = 1001,
        UnexpectedEof = 1002,
        InvalidToken = 1010,
        InvalidToken_Double = 1011,
        InvalidToken_String = 1012,
        SelectStarWithNoFrom = 1021,
        QueryTooComplex = 1030,

        // Binder Errors: 2000 -> 2999
        IdentifierResolveError = 2001,
        UserDefinedFunctionRefResolveError = 2003,
        ParameterResolveError = 2004,
        BuiltinFunctionResolveError = 2005,
        DuplicateInputSetName = 2021,
        DuplicatePropertyName = 2022,
        InvalidOrderByItemCount = 2031,
        InvalidOrderByItemExpression = 2032,
        InvalidSelectStar = 2040,
        IncorrectArgumentCount = 2050,
        InvalidTopCountValue = 2060,
        InvalidLimitCountValue = 2061,
        InvalidOffsetCountValue = 2062,
        NestedAggregateExpression = 2101,
        NonAggregatedPropertyRef = 2102,
        AggregateInWhereClause = 2103,
        AggregateInOrderByClause = 2104,
        GroupByExpressionMustContainPropertyRef = 2110,
        GroupByContainsAggregateOrSubquery = 2111,
        OrderByWithGroupByNotSupported = 2120,
        ScalarSubqueryCardinalityViolation = 2201,
        OrderByInSubqueryNotSupported = 2202,
        InvalidSelectTopInSubquery = 2203,
        InvalidOffsetLimitInSubquery = 2204,
        TopNotAllowedWithOffsetLimit = 2205,
        UnsupportedOrderByExpression = 2206,

        // C* Binder Errors
        InvalidTypeNumber = 2700,
        InvalidTypeArray = 2701,
        InvalidCMapTuple = 2750,
        InvalidNullInCCollection = 2751,
        InvalidArrayContainsFunction = 2752,

        CurrentlyUnsupported = 2990,

        // Compiler Errors: 3000 -> 3999
        QueryExceededMaxJoinCount = 3002,
        QueryExceededMaxUdfRefCount = 3005,
        QueryTextExceededMaxLength = 3020,
        QueryContainingUdfNotAllowed = 3030,
        QueryExceededMongoEvalComplexity = 3031,

        // Runtime Errors: 4000 -> 4999
        UnsupportedNumericValue = 4001,
    }
}