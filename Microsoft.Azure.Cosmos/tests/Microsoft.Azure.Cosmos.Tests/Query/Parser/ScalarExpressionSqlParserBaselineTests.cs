namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class ScalarExpressionSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void All()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "ALL(SELECT *)"),
                CreateInput(description: "case insensitive", scalarExpression: "aLl(SELECT *)"),
                CreateInput(description: "nested", scalarExpression:"ALL( SELECT * WHERE ALL( SELECT *))"),
                CreateInput(
                    description: "multiple nested",
                    scalarExpression:
                "ALL( "                         +
                "   SELECT * "                  +
                "   WHERE ALL( "                +
                "       SELECT *"               +
                "       WHERE ALL("             +
                "           SELECT *"           +
                "           WHERE ALL("         +
                "               SELECT VALUE 1" +
                "           )"                  +
                "       )"                      +
                "   )"                          +
                ")"),

                // Negative
                CreateInput(description: "No closing parens", scalarExpression: "ALL(SELECT *")
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ArrayCreate()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Empty Array", scalarExpression: "[]"),
                CreateInput(description: "Single Item Array", scalarExpression: "[1]"),
                CreateInput(description: "Multi Item Array", scalarExpression: "[1, 2, 3]"),

                // Negative
                CreateInput(description: "No closing brace", scalarExpression: "["),
                CreateInput(description: "No opening brace", scalarExpression: "]"),
                CreateInput(description: "Trailing delimiter", scalarExpression: "[1,]"),
                CreateInput(description: "Delimiter but no items", scalarExpression: "[,]"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Array()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "ARRAY(SELECT *)"),

                // Negative
                CreateInput(description: "No closing parens", scalarExpression: "ARRAY(SELECT *"),
                CreateInput(description: "not a subquery", scalarExpression: "ARRAY(123)"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Between()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Regular Betweeen", scalarExpression: "42 BETWEEN 15 AND 1337"),
                CreateInput(description: "NOT Betweeen", scalarExpression: "42 NOT BETWEEN 15 AND 1337"),

                // Negative
                CreateInput(description: "Missing Betweeen", scalarExpression: "42 15 AND 1337"),
                CreateInput(description: "NOT + Missing between", scalarExpression: "42 NOT 15 AND 1337"),
                CreateInput(description: "Missing end range", scalarExpression: "42 BETWEEN 15 AND "),
                CreateInput(description: "missing start range", scalarExpression: "42 BETWEEN AND 1337"),
                CreateInput(description: "missing needle", scalarExpression: "BETWEEN 15 AND 1337"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Binary()
        {
            // Positive
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>();
            foreach (SqlBinaryScalarOperatorKind binaryOperator in (SqlBinaryScalarOperatorKind[])Enum.GetValues(typeof(SqlBinaryScalarOperatorKind)))
            {
                inputs.Add(
                    CreateInput(
                        description: binaryOperator.ToString(),
                        scalarExpression: SqlBinaryScalarExpression.Create(
                            binaryOperator,
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42)),
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(1337))).ToString()));
            }

            // Order of operations
            inputs.Add(CreateInput(
                description: "Multiplication, division, and remainder -> left to right",
                scalarExpression: "1 / 2 * 3 % 4"));
            inputs.Add(CreateInput(
                description: "Addition and subtraction -> left to right",
                scalarExpression: "1 + 2 - 3 + 4"));
            inputs.Add(CreateInput(
                description: "Relational operators -> left to right",
                scalarExpression: "1 < 2 <= 3 > 4 >= 5"));
            inputs.Add(CreateInput(
                description: "Equality operators -> left to right",
                scalarExpression: "1 = 2 != 3 = 4"));
            inputs.Add(CreateInput(
                description: "Bitwise AND > Bitwise XOR (exclusive or) > Bitwise OR (inclusive or)",
                scalarExpression: "1 | 2 & 3 ^ 4"));
            inputs.Add(CreateInput(
                description: "Logical AND > Logical OR",
                scalarExpression: "1 AND 2 OR 3 AND 4"));
            inputs.Add(CreateInput(
                description: "Conditional-expression Right to left",
                scalarExpression: "1 ? 2 : 3 + 4"));
            inputs.Add(CreateInput(
                description: "Multiplicative > Additive",
                scalarExpression: "1 + 2 * 3 - 4 / 5"));
            inputs.Add(CreateInput(
                description: "Additive > Relational",
                scalarExpression: "1 + 2 < 2 + 3 <= 10 - 4 > 5 >= 3"));
            inputs.Add(CreateInput(
                description: "Relational > Equality",
                scalarExpression: "1 > 2 = false AND 1 > 2 != true"));
            inputs.Add(CreateInput(
                description: "Equality > Bitwise AND",
                scalarExpression: "1 = 2 & 3 != 4"));
            inputs.Add(CreateInput(
                description: "Bitwise AND > Bitwise Exclusive OR",
                scalarExpression: "1 ^ 2 & 3 ^ 4"));
            inputs.Add(CreateInput(
                description: "Bitwise Exclusive OR > Bitwise Inclusive OR",
                scalarExpression: "1 | 2 ^ 3 | 4"));
            inputs.Add(CreateInput(
                description: "Bitwise Inclusive OR > Logical AND",
                scalarExpression: "1 AND 2 | 3 AND 4"));
            inputs.Add(CreateInput(
                description: "Logical AND > Logical OR",
                scalarExpression: "1 OR 2 AND 3 OR 4"));
            inputs.Add(CreateInput(
                description: "Logical OR > String Concat",
                scalarExpression: "1 || 2 OR 3 || 4"));

            // Binary with logical expressions
            inputs.Add(CreateInput(
                description: "AND with LIKE expressions",
                scalarExpression: "r.name LIKE 12  AND r.name LIKE 34"));
            inputs.Add(CreateInput(
                description: "OR with LIKE expression",
                scalarExpression: "r.name LIKE 12  OR r.name LIKE 34"));
            inputs.Add(CreateInput(
                description: "AND with IN expression",
                scalarExpression: "r.name IN (1,2,3)  AND r.name IN (4,5,6)"));
            inputs.Add(CreateInput(
                description: "OR with IN expression",
                scalarExpression: "r.name IN (1,2,3)  OR r.name IN (4,5,6)"));
            inputs.Add(CreateInput(
                description: "Double AND",
                scalarExpression: "r.age = 1 AND r.age = 2 AND r.age = 3"));
            inputs.Add(CreateInput(
                description: "Double OR",
                scalarExpression: "r.age = 1 OR r.age = 2 OR r.age = 3"));
            inputs.Add(CreateInput(
                description: "AND and then OR",
                scalarExpression: "r.age = 1 AND r.age = 2 OR r.age = 3"));
            inputs.Add(CreateInput(
                description: "OR and then AND",
                scalarExpression: "r.age = 1 OR r.age = 2 AND r.age = 3"));

            // Negative
            inputs.Add(CreateInput(description: "Missing Right", scalarExpression: "42 +"));
            inputs.Add(CreateInput(description: "Missing Left", scalarExpression: "AND 1337"));
            inputs.Add(CreateInput(description: "Unknown Operator", scalarExpression: "42 # 1337"));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Coalesce()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "42 ?? 1337"),

                // Negative
                CreateInput(description: "Missing Right", scalarExpression: "42 ??"),
                CreateInput(description: "Missing Left", scalarExpression: "?? 1337"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Conditional()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "42 ? 123 : 1337"),

                // Negative
                CreateInput(description: "Missing Condition", scalarExpression: " ? 123 : 1337"),
                CreateInput(description: "Missing if true", scalarExpression: "42 ?  : 1337"),
                CreateInput(description: "Missing if false", scalarExpression: "42 ? 123 : "),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Exists()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "EXISTS(SELECT *)"),
                CreateInput(description: "case insensitive", scalarExpression: "ExIsTS(SELECT *)"),

                // Negative
                CreateInput(description: "No closing parens", scalarExpression: "EXISTS(SELECT *"),
                CreateInput(description: "not a subquery", scalarExpression: "EXISTS(123)"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void First()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "FIRST(SELECT *)"),
                CreateInput(description: "case insensitive", scalarExpression: "FIRST(SELECT *)"),
                CreateInput(description: "nested", scalarExpression:"FIRST( SELECT * WHERE FIRST( SELECT *))"),
                CreateInput(
                    description: "multiple nested",
                    scalarExpression:
                        "FIRST( "                         +
                        "   SELECT * "                    +
                        "   WHERE FIRST( "                +
                        "       SELECT *"                 +
                        "       WHERE FIRST("             +
                        "           SELECT *"             +
                        "           WHERE FIRST("         +
                        "               SELECT VALUE 1"   +
                        "           )"                    +
                        "       )"                        +
                        "   )"                            +
                        ")"),

                // Negative
                CreateInput(description: "No closing parens", scalarExpression: "FIRST(SELECT *")
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void FunctionCall()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "ABS(-123)"),
                CreateInput(description: "No Arguments", scalarExpression: "PI()"),
                CreateInput(description: "multiple arguments", scalarExpression: "STARTSWITH('asdf', 'as')"),
                CreateInput(description: "udf", scalarExpression: "udf.my_udf(-123)"),

                // Negative
                CreateInput(description: "missing brace", scalarExpression: "ABS(-123"),
                CreateInput(description: "invalid identifier", scalarExpression: "*@#$('asdf')"),
                CreateInput(description: "trailing delimiter", scalarExpression: "ABS('asdf',)"),
                CreateInput(description: "delimiter but no arguments", scalarExpression: "ABS(,)"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void In()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "42 IN(42)"),
                CreateInput(description: "multiple arguments", scalarExpression: "42 IN (1, 2, 3)"),
                CreateInput(description: "multiple string arguments", scalarExpression: "42 IN (\"WA\", \"CA\")"),
                CreateInput(description: "NOT IN", scalarExpression: "42 NOT IN (42)"),

                // Negative
                CreateInput(description: "Empty List", scalarExpression: "42 IN()"),
                CreateInput(description: "missing brace", scalarExpression: "42 IN (-123"),
                CreateInput(description: "trailing delimiter", scalarExpression: "42 IN ('asdf',)"),
                CreateInput(description: "delimiter but no arguments", scalarExpression: "42 IN (,)"),
                CreateInput(description: "missing needle", scalarExpression: "IN (-123"),
                CreateInput(description: "missing haystack", scalarExpression: "42 IN "),
                CreateInput(description: "mispelled keyword", scalarExpression: "42 inn (123)"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Last()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "LAST(SELECT *)"),
                CreateInput(description: "case insensitive", scalarExpression: "LAST(SELECT *)"),
                CreateInput(description: "nested", scalarExpression:"LAST( SELECT * WHERE LAST( SELECT *))"),
                CreateInput(
                    description: "multiple nested",
                    scalarExpression:
                        "LAST( "                         +
                        "   SELECT * "                   +
                        "   WHERE LAST( "                +
                        "       SELECT *"                +
                        "       WHERE LAST("             +
                        "           SELECT *"            +
                        "           WHERE LAST("         +
                        "               SELECT VALUE 1"  +
                        "           )"                   +
                        "       )"                       +
                        "   )"                           +
                        ")"),

                // Negative
                CreateInput(description: "No closing parens", scalarExpression: "LAST(SELECT *")
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Literal()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Integer", scalarExpression: "42"),
                CreateInput(description: "Double", scalarExpression: "1337.42"),
                CreateInput(description: "Negative Number", scalarExpression: "-1337.42"),
                CreateInput(description: "E notation", scalarExpression: "1E2"),
                CreateInput(description: "string with double quotes", scalarExpression: "\"hello\""),
                CreateInput(description: "string with single quotes", scalarExpression: "'hello'"),
                CreateInput(description: "true", scalarExpression: "true"),
                CreateInput(description: "false", scalarExpression: "false"),
                CreateInput(description: "null", scalarExpression: "null"),
                CreateInput(description: "undefined", scalarExpression: "undefined"),

                // Negative
                CreateInput(description: "Invalid Number", scalarExpression: "-0.E"),
                CreateInput(description: "string missing quote", scalarExpression: "\"hello"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void MemberIndexer()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", scalarExpression: "c.arr[2]"),
                CreateInput(description: "Expression as indexer", scalarExpression: "c.arr[2 + 2]"),

                // Negative
                CreateInput(description: "missing indexer expression", scalarExpression: "c.array[]"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void ObjectCreate()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Empty Object", scalarExpression: "{}"),
                CreateInput(description: "Single Property", scalarExpression: "{ 'prop' : 42 }"),
                CreateInput(description: "Multiple Property", scalarExpression: "{ 'prop1' : 42, 'prop2' : 1337 }"),

                CreateInput(description: "Double Quotes", scalarExpression: "{ \"prop\" : \"Some String\" }"),
                CreateInput(description: "Single Quotes", scalarExpression: "{ 'prop' : 'Some String' }"),
                CreateInput(description: "Mixed Quotes", scalarExpression: "{ 'prop' : \"Some String\" }"),
                CreateInput(description: "Mixed Quotes 2", scalarExpression: "{ \"prop\" : 'Some String' }"),

                CreateInput(description: "Double Quotes Within Single Quotes", scalarExpression: "{ 'prop' : 'Some \"String\" Value' }"),
                CreateInput(description: "Single Quotes Within Double Quotes", scalarExpression: "{ 'prop' : \"Some 'String' Value\" }"),

                CreateInput(description: "Escaped Double Quotes Within Double Quotes", scalarExpression: "{ 'prop' : \"Some \\\"String\\\" Value\" }"),

                // Negative
                CreateInput(description: "Missing Close Brace", scalarExpression: "{"),
                CreateInput(description: "trailing delimiter", scalarExpression: "{ 'prop' : 42, }"),
                CreateInput(description: "delimiter but no properties", scalarExpression: "{ , }"),
                CreateInput(description: "non string property name", scalarExpression: "{ 23 : 42, }"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void PropertyRef()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "root", scalarExpression: "c"),
                CreateInput(description: "Basic", scalarExpression: "c.arr"),
                CreateInput(description: "ArrayIndex", scalarExpression: "c.arr[2]"),
                CreateInput(description: "StringIndex", scalarExpression: "c.arr['asdf']"),

                // Negative
                CreateInput(description: "number with dot notation", scalarExpression: "c.2"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Subquery()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "basic", scalarExpression: "(SELECT *)"),

                // Negative
                CreateInput(description: "missing right parens", scalarExpression: "(SELECT *"),
                CreateInput(description: "missing left parens", scalarExpression: "SELECT *)"),
                CreateInput(description: "missing both parens", scalarExpression: "SELECT *"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Unary()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>();
            // Positive
            foreach (SqlUnaryScalarOperatorKind unaryOperator in (SqlUnaryScalarOperatorKind[])Enum.GetValues(typeof(SqlUnaryScalarOperatorKind)))
            {
                inputs.Add(
                    CreateInput(
                        description: unaryOperator.ToString(),
                        scalarExpression: SqlUnaryScalarExpression.Create(
                            unaryOperator,
                            SqlLiteralScalarExpression.Create(SqlNumberLiteral.Create(42))).ToString()));
            }

            // Negative
            inputs.Add(CreateInput(description: "unknown operator", scalarExpression: "$42"));

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Parenthesized()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Parens", scalarExpression: "('John')"),
                CreateInput(description: "Parens force order of operations", scalarExpression: "(1 + 2) * 3"),

                // Negative
                CreateInput(description: "Mismatched", scalarExpression: "(('John')"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void OrderOfOperation()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Conditional > Coalese", scalarExpression: "1 ? 2 ?? 3 : 4"),
                CreateInput(description: "property refs > binary", scalarExpression: "x + y.foo"),
                CreateInput(description: "property refs > binary > logical and", scalarExpression: "c.name = 'John' AND c.age = 42"),
                CreateInput(description: "unary > binary", scalarExpression: "-2 < 1"),
                CreateInput(description: "propertyref > unary", scalarExpression: "NOT x.y.z"),
                CreateInput(description: "Conditional > PropertyRef", scalarExpression: "true ? {\"x\": 1} : {\"x\": 2}.x"),

                // Negative
                CreateInput(description: "Coalesce + Between", scalarExpression: "1 BETWEEN 3 ?? 4 AND 5 AND 6"),
                CreateInput(description: "In + Between", scalarExpression: "SELECT 1 NOT BETWEEN 2 AND 1 NOT IN (1, 2, 3)"),
                CreateInput(description: "Nested BETWEEN", scalarExpression: "1 BETWEEN 2 AND 3 BETWEEN 4 AND 5"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void StringLiteral()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Single quoted string literals do not allow ' even when it's escaped.
                // Parser currently fails with Antlr4.Runtime.NoViableAltException
                CreateInput(
                    description: @"Single quoted string literals with escape seqence",
                    scalarExpression: @"['\""DoubleQuote', '\\ReverseSolidus', '\/solidus', '\bBackspace', '\fSeparatorFeed', '\nLineFeed', '\rCarriageReturn', '\tTab', '\u1234']"),
                CreateInput(
                    description: @"Double quoted string literals with escape seqence",
                    scalarExpression: @"[""'SingleQuote"", ""\""DoubleQuote"", ""\\ReverseSolidus"", ""\/solidus"", ""\bBackspace"", ""\fSeparatorFeed"", ""\nLineFeed"", ""\rCarriageReturn"", ""\tTab"", ""\u1234""]"),
                CreateInput(
                    description: @"Single quoted string literals special cases",
                    scalarExpression: @"['\""', '\""\""', '\\', '\\\\', '\/', '\/\/', '\b', '\b\b', '\f', '\f\f', '\n', '\n\n', '\r', '\r\r', '\t', '\t\t', '\u1234', '\u1234\u1234']"),
                CreateInput(
                    description: @"Double quoted string literals special cases",
                    scalarExpression: @"[""\"""", ""\""\"""", ""\\"", ""\\\\"", ""\/"", ""\/\/"", ""\b"", ""\b\b"", ""\f"", ""\f\f"", ""\n"", ""\n\n"", ""\r"", ""\r\r"", ""\t"", ""\t\t"", ""\u1234"", ""\u1234\u1234""]"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string scalarExpression)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT VALUE {scalarExpression}");
        }
    }
}