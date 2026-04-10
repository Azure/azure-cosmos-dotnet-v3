//-----------------------------------------------------------------------
// <copyright file="JTokenEqualityComparerUnitTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// JTokenEqualityComparerUnitTests Class.
    /// </summary>
    [TestClass]
    public class ScalarExpressionEvaluatorTests
    {
        private static readonly CosmosElement Undefined = CosmosUndefined.Create();

        [TestMethod]
        [Owner("brchon")]
        public void SqlArrayCreateScalarExpressionTest()
        {
            SqlArrayCreateScalarExpression inner = SqlArrayCreateScalarExpression.Create(
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3)));
            AssertEvaluation(
                CosmosArray.Create(
                    new List<CosmosElement>()
                    {
                        CosmosNumber64.Create(1),
                        CosmosNumber64.Create(2),
                        CosmosNumber64.Create(3),
                    }),
                inner);

            SqlArrayCreateScalarExpression outer = SqlArrayCreateScalarExpression.Create(inner);
            AssertEvaluation(
                CosmosArray.Create(
                    new List<CosmosElement>() { CosmosArray.Create(
                        new List<CosmosElement>()
                        {
                            CosmosNumber64.Create(1),
                            CosmosNumber64.Create(2),
                            CosmosNumber64.Create(3),
                        })}),
                outer);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlArrayScalarExpressionTest()
        {
            CosmosObject tag = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("asdf")
            });

            CosmosObject tags = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["tags"] = CosmosArray.Create(new List<CosmosElement>() { tag }),
                ["_rid"] = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==")
            });

            CosmosObject tagsWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = tags,
                ["_rid"] = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==")
            });

            // ARRAY(SELECT VALUE t.name FROM t in c.tags)
            SqlArrayScalarExpression arrayScalarExpression = SqlArrayScalarExpression.Create(
                SqlQuery.Create(
                    SqlSelectClause.Create(
                        SqlSelectValueSpec.Create(
                            TestUtils.CreatePathExpression("t", "name"))),
                    SqlFromClause.Create(
                        SqlArrayIteratorCollectionExpression.Create(
                            SqlIdentifier.Create("t"),
                            SqlInputPathCollection.Create(
                                SqlIdentifier.Create("c"),
                                SqlStringPathExpression.Create(
                                    null,
                                    SqlStringLiteral.Create("tags"))))),
                    whereClause: null,
                    groupByClause: null,
                    orderByClause: null,
                    offsetLimitClause: null));

            CosmosArray tagNames = CosmosArray.Create(new List<CosmosElement>() { CosmosString.Create("asdf") });
            AssertEvaluation(tagNames, arrayScalarExpression, tagsWrapped);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlBetweenScalarExpressionTest()
        {
            SqlBetweenScalarExpression threeBetweenFourAndFive = SqlBetweenScalarExpression.Create(
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(4)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)),
                not: false);
            AssertEvaluation(CosmosBoolean.Create(true), threeBetweenFourAndFive);

            SqlBetweenScalarExpression threeNotBetweenFourAndFive = SqlBetweenScalarExpression.Create(
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(4)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5)),
                not: true);
            AssertEvaluation(CosmosBoolean.Create(false), threeNotBetweenFourAndFive);

            SqlBetweenScalarExpression trueBetweenTrueAndTrueNested = SqlBetweenScalarExpression.Create(
                SqlLiteralScalarExpression.Create(SqlBooleanLiteral.Create(true)),
                threeBetweenFourAndFive,
                threeBetweenFourAndFive,
                not: false);
            AssertEvaluation(CosmosBoolean.Create(true), trueBetweenTrueAndTrueNested);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlBinaryScalarExpressionTest()
        {
            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));
            SqlLiteralScalarExpression hello = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello"));
            SqlLiteralScalarExpression world = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"));
            SqlLiteralScalarExpression trueBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.True);
            SqlLiteralScalarExpression falseBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.False);
            SqlLiteralScalarExpression nullLiteral = SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton);
            SqlLiteralScalarExpression undefinedLiteral = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());

            SqlArrayCreateScalarExpression arrayCreateScalarExpresion1 = SqlArrayCreateScalarExpression.Create();
            SqlArrayCreateScalarExpression arrayCreateScalarExpresion2 = SqlArrayCreateScalarExpression.Create(five);
            SqlObjectCreateScalarExpression objectCreateScalarExpression = SqlObjectCreateScalarExpression.Create();

            SqlBinaryScalarExpression fivePlusThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Add, five, three);
            AssertEvaluation(CosmosNumber64.Create(3 + 5), fivePlusThree);

            SqlBinaryScalarExpression trueAndFalse = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, trueBoolean, falseBoolean);
            AssertEvaluation(CosmosBoolean.Create(true && false), trueAndFalse);

            SqlBinaryScalarExpression falseAndUndefined = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, falseBoolean, undefinedLiteral);
            AssertEvaluation(CosmosBoolean.Create(false), falseAndUndefined);

            SqlBinaryScalarExpression trueAndUndefined = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.And, trueBoolean, undefinedLiteral);
            AssertEvaluation(Undefined, trueAndUndefined);

            try
            {
                SqlBinaryScalarExpression threeBitwiseAndFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseAnd, three, five);
                AssertEvaluation(CosmosNumber64.Create(1), threeBitwiseAndFive);
            }
            catch (Exception)
            {
            }

            try
            {
                SqlBinaryScalarExpression threeBitwiseOrFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseOr, three, five);
                AssertEvaluation(CosmosNumber64.Create(7), threeBitwiseOrFive);
            }
            catch (Exception)
            {
            }

            try
            {
                SqlBinaryScalarExpression threeBitwiseXorFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.BitwiseXor, three, five);
                AssertEvaluation(CosmosNumber64.Create(7), threeBitwiseXorFive);
            }
            catch (Exception)
            {
            }

            SqlBinaryScalarExpression nullCoalesceFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Coalesce, nullLiteral, five);
            AssertEvaluation(CosmosNull.Create(), nullCoalesceFive);

            SqlBinaryScalarExpression fiveDivideFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Divide, five, five);
            AssertEvaluation(CosmosNumber64.Create(5 / 5), fiveDivideFive);

            SqlBinaryScalarExpression fiveEqualFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Equal, five, five);
            AssertEvaluation(CosmosBoolean.Create(5 == 5), fiveEqualFive);

            SqlBinaryScalarExpression threeEqualFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Equal, three, five);
            AssertEvaluation(CosmosBoolean.Create(3 == 5), threeEqualFive);

            AssertEvaluation(
                CosmosBoolean.Create(true),
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.Equal,
                    arrayCreateScalarExpresion1,
                    arrayCreateScalarExpresion1));

            AssertEvaluation(
                CosmosBoolean.Create(false),
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.Equal,
                    arrayCreateScalarExpresion1,
                    arrayCreateScalarExpresion2));

            AssertEvaluation(
                CosmosBoolean.Create(false),
                SqlBinaryScalarExpression.Create(
                    SqlBinaryScalarOperatorKind.Equal,
                    arrayCreateScalarExpresion1,
                    objectCreateScalarExpression));

            SqlBinaryScalarExpression threeGreaterThanFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThan, three, five);
            AssertEvaluation(CosmosBoolean.Create(3 > 5), threeGreaterThanFive);

            SqlBinaryScalarExpression fiveGreaterThanThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThan, five, three);
            AssertEvaluation(CosmosBoolean.Create(5 > 3), fiveGreaterThanThree);

            SqlBinaryScalarExpression threeGreaterThanOrEqualFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThanOrEqual, three, five);
            AssertEvaluation(CosmosBoolean.Create(3 >= 5), threeGreaterThanOrEqualFive);

            SqlBinaryScalarExpression fiveGreaterThanOrEqualThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.GreaterThanOrEqual, five, three);
            AssertEvaluation(CosmosBoolean.Create(5 >= 3), fiveGreaterThanOrEqualThree);

            SqlBinaryScalarExpression threeLessThanFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThan, three, five);
            AssertEvaluation(CosmosBoolean.Create(3 < 5), threeLessThanFive);

            SqlBinaryScalarExpression fiveLessThanThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThan, five, three);
            AssertEvaluation(CosmosBoolean.Create(5 < 3), fiveLessThanThree);

            SqlBinaryScalarExpression threeLessThanOrEqualFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThanOrEqual, three, five);
            AssertEvaluation(CosmosBoolean.Create(3 <= 5), threeLessThanOrEqualFive);

            SqlBinaryScalarExpression fiveLessThanOrEqualThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.LessThanOrEqual, five, three);
            AssertEvaluation(CosmosBoolean.Create(5 <= 3), fiveLessThanOrEqualThree);

            SqlBinaryScalarExpression fiveModThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Modulo, five, three);
            AssertEvaluation(CosmosNumber64.Create(5 % 3), fiveModThree);

            SqlBinaryScalarExpression fiveMultiplyThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Multiply, five, three);
            AssertEvaluation(CosmosNumber64.Create(5 * 3), fiveMultiplyThree);

            SqlBinaryScalarExpression fiveNotEqualThree = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.NotEqual, five, three);
            AssertEvaluation(CosmosBoolean.Create(5 != 3), fiveNotEqualThree);

            SqlBinaryScalarExpression fiveNotEqualFive = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.NotEqual, five, five);
            AssertEvaluation(CosmosBoolean.Create(5 != 5), fiveNotEqualFive);

            SqlBinaryScalarExpression trueOrFalse = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Or, trueBoolean, falseBoolean);
            AssertEvaluation(CosmosBoolean.Create(true || false), trueOrFalse);

            SqlBinaryScalarExpression trueOrUndefined = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Or, trueBoolean, undefinedLiteral);
            AssertEvaluation(CosmosBoolean.Create(true), trueOrUndefined);

            SqlBinaryScalarExpression falseOrUndefined = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Or, falseBoolean, undefinedLiteral);
            AssertEvaluation(Undefined, falseOrUndefined);

            SqlBinaryScalarExpression helloConcatWorld = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.StringConcat, hello, world);
            AssertEvaluation(CosmosString.Create("Hello" + "World"), helloConcatWorld);

            SqlBinaryScalarExpression fiveSubtract3 = SqlBinaryScalarExpression.Create(SqlBinaryScalarOperatorKind.Subtract, five, three);
            AssertEvaluation(CosmosNumber64.Create(5 - 3), fiveSubtract3);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlCoalesceScalarExpressionTest()
        {
            SqlLiteralScalarExpression nullLiteral = SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton);
            SqlLiteralScalarExpression undefinedLiteral = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());
            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));

            SqlCoalesceScalarExpression nullCoalesceFive = SqlCoalesceScalarExpression.Create(nullLiteral, five);
            AssertEvaluation(CosmosNull.Create(), nullCoalesceFive);

            SqlCoalesceScalarExpression undefinedCoalesceFive = SqlCoalesceScalarExpression.Create(undefinedLiteral, five);
            AssertEvaluation(CosmosNumber64.Create(5), undefinedCoalesceFive);

            SqlCoalesceScalarExpression threeCoalesceFive = SqlCoalesceScalarExpression.Create(three, five);
            AssertEvaluation(CosmosNumber64.Create(3), threeCoalesceFive);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlConditionalScalarExpressionTest()
        {
            SqlLiteralScalarExpression nullLiteral = SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton);
            SqlLiteralScalarExpression undefinedLiteral = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());
            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));

            SqlConditionalScalarExpression nullConditionalThreeFive = SqlConditionalScalarExpression.Create(nullLiteral, three, five);
            AssertEvaluation(CosmosNumber64.Create(5), nullConditionalThreeFive);

            SqlConditionalScalarExpression undefinedCoalesceFive = SqlConditionalScalarExpression.Create(undefinedLiteral, three, five);
            AssertEvaluation(CosmosNumber64.Create(5), undefinedCoalesceFive);

            SqlLiteralScalarExpression trueBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.True);
            SqlConditionalScalarExpression trueConditionalThreeFive = SqlConditionalScalarExpression.Create(trueBoolean, three, five);
            AssertEvaluation(CosmosNumber64.Create(3), trueConditionalThreeFive);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlExistsScalarExpressionTest()
        {
            CosmosObject tag = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("asdf")
            });

            CosmosObject tags = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["tags"] = CosmosArray.Create(new List<CosmosElement>() { tag }),
                ["_rid"] = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==")
            });

            CosmosObject tagsWrapped = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = tags,
                ["_rid"] = CosmosString.Create("AYIMAMmFOw8YAAAAAAAAAA==")
            });

            // EXISTS(SELECT VALUE t.name FROM t in c.tags where t.name = "asdf")
            SqlExistsScalarExpression existsScalarExpressionMatched = SqlExistsScalarExpression.Create(
                SqlQuery.Create(
                    SqlSelectClause.Create(
                        SqlSelectValueSpec.Create(
                            TestUtils.CreatePathExpression("t", "name"))),
                    SqlFromClause.Create(
                        SqlArrayIteratorCollectionExpression.Create(
                            SqlIdentifier.Create("t"),
                            SqlInputPathCollection.Create(
                                SqlIdentifier.Create("c"),
                                SqlStringPathExpression.Create(
                                    null,
                                    SqlStringLiteral.Create("tags"))))),
                    SqlWhereClause.Create(
                        SqlBinaryScalarExpression.Create(
                            SqlBinaryScalarOperatorKind.Equal,
                            TestUtils.CreatePathExpression("t", "name"),
                            SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("asdf")))),
                    groupByClause: null,
                    orderByClause: null,
                    offsetLimitClause: null));

            AssertEvaluation(CosmosBoolean.Create(true), existsScalarExpressionMatched, tagsWrapped);

            SqlExistsScalarExpression existsScalarExpressionNotMatched = SqlExistsScalarExpression.Create(
                SqlQuery.Create(
                    SqlSelectClause.Create(
                        SqlSelectValueSpec.Create(
                            TestUtils.CreatePathExpression("t", "name"))),
                    SqlFromClause.Create(
                        SqlArrayIteratorCollectionExpression.Create(
                            SqlIdentifier.Create("t"),
                            SqlInputPathCollection.Create(
                                SqlIdentifier.Create("c"),
                                SqlStringPathExpression.Create(
                                    null,
                                    SqlStringLiteral.Create("tags"))))),
                    SqlWhereClause.Create(
                        SqlBinaryScalarExpression.Create(
                            SqlBinaryScalarOperatorKind.NotEqual,
                            TestUtils.CreatePathExpression("t", "name"),
                            SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("asdf")))),
                    groupByClause: null,
                    orderByClause: null,
                    offsetLimitClause: null));

            AssertEvaluation(CosmosBoolean.Create(false), existsScalarExpressionNotMatched, tagsWrapped);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlInScalarExpressionTest()
        {
            SqlLiteralScalarExpression one = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1));
            SqlLiteralScalarExpression two = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));

            SqlInScalarExpression oneInOneTwoThree = SqlInScalarExpression.Create(one, false, one, two, three);
            AssertEvaluation(CosmosBoolean.Create(true), oneInOneTwoThree);

            SqlInScalarExpression oneNotInOneTwoThree = SqlInScalarExpression.Create(one, true, one, two, three);
            AssertEvaluation(CosmosBoolean.Create(false), oneNotInOneTwoThree);

            SqlInScalarExpression oneInTwoThree = SqlInScalarExpression.Create(one, false, two, three);
            AssertEvaluation(CosmosBoolean.Create(false), oneInTwoThree);

            SqlInScalarExpression oneNotInTwoThree = SqlInScalarExpression.Create(one, true, two, three);
            AssertEvaluation(CosmosBoolean.Create(true), oneNotInTwoThree);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlLiteralScalarExpressionTest()
        {
            SqlLiteralScalarExpression numberLiteral = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1));
            AssertEvaluation(CosmosNumber64.Create(1), numberLiteral);

            SqlLiteralScalarExpression stringLiteral = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello"));
            AssertEvaluation(CosmosString.Create("Hello"), stringLiteral);

            SqlLiteralScalarExpression trueLiteral = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.True);
            AssertEvaluation(CosmosBoolean.Create(true), trueLiteral);

            SqlLiteralScalarExpression falseLiteral = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.False);
            AssertEvaluation(CosmosBoolean.Create(false), falseLiteral);

            SqlLiteralScalarExpression nullLiteral = SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton);
            AssertEvaluation(CosmosNull.Create(), nullLiteral);

            SqlLiteralScalarExpression undefinedLiteral = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());
            AssertEvaluation(Undefined, undefinedLiteral);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlMemberIndexerScalarExpressionTest()
        {
            CosmosObject alice = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("Alice")
            });

            CosmosObject bob = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("Bob")
            });

            CosmosObject document = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("John"),
                ["kids"] = CosmosArray.Create(new List<CosmosElement>() { alice, bob })
            });

            CosmosObject wrappedDocument = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["c"] = document
            });

            AssertEvaluation(CosmosString.Create("John"), TestUtils.CreatePathExpression("c", "name"), wrappedDocument);

            AssertEvaluation(alice, TestUtils.CreatePathExpression("c", "kids", 0), wrappedDocument);

            AssertEvaluation(CosmosString.Create("Alice"), TestUtils.CreatePathExpression("c", "kids", 0, "name"), wrappedDocument);

            // Indexing by a path that does not exist.
            AssertEvaluation(Undefined, TestUtils.CreatePathExpression("c", "NotARealPath"), wrappedDocument);

            // Indexing an object with a number.
            AssertEvaluation(Undefined, TestUtils.CreatePathExpression("c", 5), wrappedDocument);

            // Indexing an array with a string.
            AssertEvaluation(Undefined, TestUtils.CreatePathExpression("c", "kids", "invalid string index", "name"), wrappedDocument);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlObjectCreateScalarExpressionTest()
        {
            CosmosObject expected = CosmosObject.Create(new Dictionary<string, CosmosElement>
            {
                ["name"] = CosmosString.Create("John")
            });

            SqlObjectCreateScalarExpression john = SqlObjectCreateScalarExpression.Create(
                SqlObjectProperty.Create(SqlPropertyName.Create("name"),
                SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("John"))));
            AssertEvaluation(expected, john);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlPropertyRefScalarExpressionTest()
        {
            // To lazy to test this right now since I already tested MemberRef
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlSubqueryScalarExpressionTest()
        {
            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression three = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(3));

            // (SELECT VALUE 5 + 3)
            SqlSubqueryScalarExpression subqueryScalarExpression = SqlSubqueryScalarExpression.Create(
                SqlQuery.Create(
                    SqlSelectClause.Create(
                        SqlSelectValueSpec.Create(
                            SqlBinaryScalarExpression.Create(
                                SqlBinaryScalarOperatorKind.Add,
                                five,
                                three))),
                    fromClause: null,
                    whereClause: null,
                    groupByClause: null,
                    orderByClause: null,
                    offsetLimitClause: null));

            AssertEvaluation(CosmosNumber64.Create(5 + 3), subqueryScalarExpression);
        }

        [TestMethod]
        [Owner("brchon")]
        public void SqlUnaryScalarExpressionTest()
        {
            SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
            SqlLiteralScalarExpression trueBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.True);

            {
                SqlUnaryScalarExpression bitwiseNot = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.BitwiseNot, five);
                AssertEvaluation(CosmosNumber64.Create(~5), bitwiseNot);
            }

            {
                SqlLiteralScalarExpression largeNumber = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(130679749712577953));
                SqlUnaryScalarExpression bitwiseNot = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.BitwiseNot, largeNumber);
                AssertEvaluation(CosmosNumber64.Create(-1022657953), bitwiseNot);
            }

            {
                SqlUnaryScalarExpression not = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Not, trueBoolean);
                AssertEvaluation(CosmosBoolean.Create(!true), not);
            }

            {
                SqlUnaryScalarExpression minus = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Minus, five);
                AssertEvaluation(CosmosNumber64.Create(-5), minus);
            }

            {
                SqlUnaryScalarExpression plus = SqlUnaryScalarExpression.Create(SqlUnaryScalarOperatorKind.Plus, five);
                AssertEvaluation(CosmosNumber64.Create(5), plus);
            }
        }

        private static void AssertEvaluation(
            CosmosElement expected,
            SqlScalarExpression sqlScalarExpression,
            CosmosElement document = null)
        {
            CosmosElement evaluation = sqlScalarExpression.Accept(ScalarExpressionEvaluator.Singleton, document);
            Assert.AreEqual(expected, evaluation);
        }
    }
}