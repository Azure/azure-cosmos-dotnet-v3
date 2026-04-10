//-----------------------------------------------------------------------
// <copyright file="BuiltinFunctionEvaluatorTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Query.OfflineEngineTests
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.CosmosElements.Numbers;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.Azure.Cosmos.Tests.Query.OfflineEngine;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json.Linq;

    [TestClass]
    public class BuiltinFunctionEvaluatorTests
    {
        private static readonly CosmosElement Undefined = CosmosUndefined.Create();

        private static readonly SqlLiteralScalarExpression five = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(5));
        private static readonly SqlLiteralScalarExpression six = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(6));
        private static readonly SqlLiteralScalarExpression negativeThree = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-3));
        private static readonly SqlLiteralScalarExpression floatingPoint = SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1.5));
        private static readonly SqlLiteralScalarExpression hello = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello"));
        private static readonly SqlLiteralScalarExpression world = SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("World"));
        private static readonly SqlLiteralScalarExpression trueBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.True);
        private static readonly SqlLiteralScalarExpression falseBoolean = SqlLiteralScalarExpression.Create(SqlBooleanLiteral.False);
        private static readonly SqlLiteralScalarExpression nullLiteral = SqlLiteralScalarExpression.Create(SqlNullLiteral.Singleton);
        private static readonly SqlLiteralScalarExpression undefinedLiteral = SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create());

        //TODO: add sanity tests for the other builtin functions

        [TestMethod]
        public void ABS()
        {
            AssertEvaluation(
                CosmosNumber64.Create(Math.Abs(5)),
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Abs,
                    five));
        }

        [TestMethod]
        public void ARRAY_CONCAT()
        {
            // ARRAY_CONCAT(["apples"], ["strawberries"], ["bananas"])
            AssertEvaluation(
                CosmosArray.Create(CosmosString.Create("apples"), CosmosString.Create("strawberries"), CosmosString.Create("bananas")),
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayConcat,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples")),
                    JTokenToSqlScalarExpression.Convert(new JArray("strawberries")),
                    JTokenToSqlScalarExpression.Convert(new JArray("bananas"))));

            // ARRAY_CONCAT("apples", ["strawberries"], ["bananas"])
            AssertEvaluation(
                Undefined,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayConcat,
                    JTokenToSqlScalarExpression.Convert("apples"),
                    JTokenToSqlScalarExpression.Convert(new JArray("strawberries")),
                    JTokenToSqlScalarExpression.Convert(new JArray("bananas"))));

            // ARRAY_CONCAT(undefined, ["strawberries"], ["bananas"])
            AssertEvaluation(
                Undefined,
                SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayConcat,
                    SqlLiteralScalarExpression.SqlUndefinedLiteralScalarExpression,
                    JTokenToSqlScalarExpression.Convert(new JArray("strawberries")),
                    JTokenToSqlScalarExpression.Convert(new JArray("bananas"))));
        }

        [TestMethod]
        public void ARRAY_CONTAINS()
        {
            // ARRAY_CONTAINS(["apples", "strawberries", "bananas"], "apples")
            AssertEvaluation(
                expected: CosmosBoolean.Create(new string[] { "apples", "strawberries", "bananas" }.Contains("apples")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("apples"))));

            // ARRAY_CONTAINS(["apples", "strawberries", "bananas"], "mangoes")
            AssertEvaluation(
                expected: CosmosBoolean.Create(new string[] { "apples", "strawberries", "bananas" }.Contains("mangoes")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("mangoes"))));

            // ARRAY_CONTAINS([{"name": "apples", "fresh": true}, {"name": "strawberries", "fresh": true}], {"name": "apples"}, true)
            AssertEvaluation(
                expected: CosmosBoolean.Create(true),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray(
                        JObject.FromObject(new { name = "apples", fresh = true }),
                        JObject.FromObject(new { name = "strawberries", fresh = true }))),
                    JTokenToSqlScalarExpression.Convert(JObject.FromObject(new { name = "apples" })),
                    trueBoolean));

            // ARRAY_CONTAINS([{"name": "apples", "fresh": true}, {"name": "strawberries", "fresh": true}], {"name": "apples"}, undefined)
            AssertEvaluation(
                expected: CosmosBoolean.Create(false),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray(
                        JObject.FromObject(new { name = "apples", fresh = true }),
                        JObject.FromObject(new { name = "strawberries", fresh = true }))),
                    JTokenToSqlScalarExpression.Convert(JObject.FromObject(new { name = "apples" })),
                    SqlLiteralScalarExpression.Create(SqlUndefinedLiteral.Create())));

            // ARRAY_CONTAINS([{"name": "apples", "fresh": true}, {"name": "strawberries", "fresh": true}], {"name": "apples"})
            AssertEvaluation(
                expected: CosmosBoolean.Create(false),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray(
                        JObject.FromObject(new { name = "apples", fresh = true }),
                        JObject.FromObject(new { name = "strawberries", fresh = true }))),
                    JTokenToSqlScalarExpression.Convert(JObject.FromObject(new { name = "apples" }))));

            // ARRAY_CONTAINS([{"name": "apples", "fresh": true}, {"name": "strawberries", "fresh": true}], {"name": "mangoes"}, true)
            AssertEvaluation(
                expected: CosmosBoolean.Create(false),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    JTokenToSqlScalarExpression.Convert(new JArray(
                        JObject.FromObject(new { name = "apples", fresh = true }),
                        JObject.FromObject(new { name = "strawberries", fresh = true }))),
                    JTokenToSqlScalarExpression.Convert(JObject.FromObject(new { name = "mangoes" })),
                    trueBoolean));

            // ARRAY_CONTAINS([SQUARE(2), POWER(2, 2)] 2)
            AssertEvaluation(
                expected: CosmosBoolean.Create(new double[] { 2 * 2, Math.Pow(2, 2) }.Contains(2)),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArrayContains,
                    SqlArrayCreateScalarExpression.Create(
                        SqlFunctionCallScalarExpression.CreateBuiltin(
                            SqlFunctionCallScalarExpression.Identifiers.Square,
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2))),
                        SqlFunctionCallScalarExpression.CreateBuiltin(
                            SqlFunctionCallScalarExpression.Identifiers.Power,
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)),
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2)))),
                    JTokenToSqlScalarExpression.Convert(2)));
        }

        [TestMethod]
        public void ARRAY_SLICE()
        {
            // ARRAY_SLICE(["apples", "strawberries", "bananas"], 1)
            AssertEvaluation(
                expected: CosmosArray.Create(CosmosString.Create("strawberries"), CosmosString.Create("bananas")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], 1, 1)
            AssertEvaluation(
                expected: CosmosArray.Create(CosmosString.Create("strawberries")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], -2, 1)
            AssertEvaluation(
                expected: CosmosArray.Create(CosmosString.Create("strawberries")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-2)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], -2, 2)
            AssertEvaluation(
                expected: CosmosArray.Create(CosmosString.Create("strawberries"), CosmosString.Create("bananas")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-2)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], 1, 0)
            AssertEvaluation(
                expected: CosmosArray.Create(),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], 1, 0)
            AssertEvaluation(
                expected: CosmosArray.Create(CosmosString.Create("strawberries"), CosmosString.Create("bananas")),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1000))));

            // ARRAY_SLICE(["apples", "strawberries", "bananas"], 1, 0)
            AssertEvaluation(
                expected: CosmosArray.Create(),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.ArraySlice,
                    JTokenToSqlScalarExpression.Convert(new JArray("apples", "strawberries", "bananas")),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1)),
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-100))));
        }

        [TestMethod]
        public void CONTAINS()
        {
            // CONTAINS("hello", "")
            // -> all strings contain empty string.
            AssertEvaluation(
                expected: CosmosBoolean.Create(true),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Contains,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("Hello")),
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(string.Empty))));
        }

        [TestMethod]
        public void LEFT()
        {
            // LEFT("Hello", -3)
            AssertEvaluation(
                expected: CosmosString.Empty,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Left,
                    hello,
                    negativeThree));

            // LEFT("Hello", 0)
            AssertEvaluation(
                expected: CosmosString.Empty,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Left,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0))));

            // LEFT("Hello", 2)
            AssertEvaluation(
                expected: CosmosString.Create("He"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Left,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2))));

            // LEFT("Hello", 6)
            AssertEvaluation(
                expected: CosmosString.Create("Hello"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Left,
                    hello,
                    six));
        }

        [TestMethod]
        public void REPLACE()
        {
            // REPLACE("Hello", "", "World")
            // replacing the empty string within a string is undefined behavior
            // SQL Server just returns the original string and that's the behavior the backend matches
            AssertEvaluation(
                expected: Undefined,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replace,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create(string.Empty)),
                    world));
        }

        [TestMethod]
        public void REPLICATE()
        {
            // REPLICATE("Hello", -1)
            // -> undefined
            AssertEvaluation(
                expected: Undefined,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replicate,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(-1))));

            // REPLICATE("Hello", 1.5)
            // -> REPLICATE("Hello", 1) due to truncation
            AssertEvaluation(
                expected: CosmosString.Create("Hello"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replicate,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1.5))));

            // REPLICATE("Hello", 10000)
            // -> undefined due to 10kb string cap
            AssertEvaluation(
                expected: Undefined,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replicate,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(10000))));

            // REPLICATE("Hello", EXP(400))
            // -> undefined due to 10kb string cap
            AssertEvaluation(
                expected: Undefined,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Replicate,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(Math.Exp(400)))));
        }

        [TestMethod]
        public void RIGHT()
        {
            // Right("Hello", -3)
            AssertEvaluation(
                expected: CosmosString.Empty,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    negativeThree));

            // Right("Hello", 1.5)
            AssertEvaluation(
                expected: Undefined,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    floatingPoint));

            // Right("Hello", 0)
            AssertEvaluation(
                expected: CosmosString.Empty,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(0))));

            // Right("Hello", 2)
            AssertEvaluation(
                expected: CosmosString.Create("lo"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(2))));

            // Right("Hello", 6)
            AssertEvaluation(
                expected: CosmosString.Create("Hello"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    six));

            // Right("Hello", int.MaxValue + 1)
            AssertEvaluation(
                expected: CosmosString.Create("Hello"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Right,
                    hello,
                    SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create((long)int.MaxValue + 1))));
        }

        [TestMethod]
        public void ROUND()
        {
            // ROUND(4.84090499760142E+15) 
            // offline engine has double precision errors
            AssertEvaluation(
                expected: CosmosNumber64.Create(4.84090499760142E+15),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Round,
                    SqlLiteralScalarExpression.Create(
                        SqlNumberLiteral.Create(
                            JToken.Parse("4840904997601420").Value<double>()))));

            // ROUND(0.5, 1)
            // -> should round up
            AssertEvaluation(
                expected: CosmosNumber64.Create(1),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Round,
                    SqlLiteralScalarExpression.Create(
                        SqlNumberLiteral.Create(0.5))));
        }

        [TestMethod]
        public void SUBSTRING()
        {
            // Substring("Hello", -3, 5)
            AssertEvaluation(
                expected: CosmosString.Empty,
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Substring,
                    hello,
                    negativeThree,
                    five));
        }

        [TestMethod]
        [Ignore]
        public void UPPER()
        {
            // UPPER("\u00B5")
            // MICRO SIGN does not have an upper casing
            AssertEvaluation(
                expected: CosmosString.Create("\u00B5"),
                sqlScalarExpression: SqlFunctionCallScalarExpression.CreateBuiltin(
                    SqlFunctionCallScalarExpression.Identifiers.Upper,
                    SqlLiteralScalarExpression.Create(SqlStringLiteral.Create("\u00B5"))));
        }

        private static void AssertEvaluation(
            CosmosElement expected,
            SqlScalarExpression sqlScalarExpression,
            CosmosElement document = null)
        {
            CosmosElement evaluation = sqlScalarExpression.Accept(
                ScalarExpressionEvaluator.Singleton,
                document);
            Assert.AreEqual(expected, evaluation);
        }
    }
}