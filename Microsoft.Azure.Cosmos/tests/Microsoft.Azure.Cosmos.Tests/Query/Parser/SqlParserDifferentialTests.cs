//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Parses the same queries with the ANTLR parser and the Parlot parser and compares the results.
    /// </summary>
    [TestClass]
    public sealed class SqlParserDifferentialTests
    {
        private static readonly string[] EdgeCases = new string[]
        {
            // Lexer: signs belong to numeric literals when a digit follows.
            "SELECT 1-1", "SELECT 1 -1", "SELECT 1 - 1", "SELECT -1", "SELECT - 1", "SELECT --1", "SELECT - -1", "SELECT -.5",
            "SELECT +.5e3", "SELECT 1+-1", "SELECT 1+ -1", "SELECT c-1 FROM c", "SELECT c -1 FROM c", "SELECT c - 1 FROM c",
            "SELECT -c FROM c", "SELECT 1.", "SELECT 1.e5", "SELECT 1e", "SELECT 1e+", "SELECT 1e-5", "SELECT 1E5", "SELECT .5",
            "SELECT 1.foo", "SELECT c.5 FROM c", "SELECT c.e5 FROM c", "SELECT 1abc", "SELECT 9223372036854775807",
            "SELECT 9223372036854775808", "SELECT -9223372036854775808", "SELECT 1e400", "SELECT 0001", "SELECT 123456789012345678",
            "SELECT 1234567890123456789", "SELECT +1", "SELECT +a", "SELECT ~1", "SELECT NOT NOT true", "SELECT not true",

            // Keywords and identifiers.
            "select * from c", "SeLeCt * FrOm c", "SELECT TRUE", "SELECT True", "SELECT true", "SELECT NULL", "SELECT Undefined",
            "SELECT UDF.f(1)", "SELECT udf.f(1)", "SELECT udf.f", "SELECT udf", "SELECT c.value FROM c", "SELECT c.select FROM c",
            "SELECT c.all FROM c", "SELECT c.first FROM c", "SELECT c.last FROM c", "SELECT selectx", "SELECT _a1", "SELECT a_",
            "SELECT left(1)", "SELECT LEFT(1, 2)", "SELECT Right('a', 1)", "SELECT left", "SELECT c.left FROM c",
            "SELECT ALL(1)", "SELECT ALL(SELECT 1)", "SELECT FIRST(SELECT VALUE 1)", "SELECT LAST", "SELECT all.all FROM all",
            "SELECT @", "SELECT @p", "SELECT @1", "SELECT @select", "SELECT TOP @p * FROM c", "SELECT TOP 1.5 * FROM c",
            "SELECT TOP -1 * FROM c", "SELECT TOP 10 * FROM c", "SELECT DISTINCT TOP 1 VALUE c FROM c",
            "SELECT * FROM c OFFSET 1 LIMIT 2", "SELECT * FROM c OFFSET @a LIMIT @b", "SELECT * FROM c OFFSET 1.5 LIMIT 2",
            "SELECT * FROM c OFFSET 99999999999999999999 LIMIT 2",

            // White space.
            "SELECT\t*\r\nFROM\nc", " SELECT 1 ", "SELECT\f1", "SELECT\v1", "SELECT 1", "SELECT 1　",

            // Strings.
            "SELECT 'a'", "SELECT \"a\"", "SELECT 'a\\'b'", "SELECT \"a\\\"b\"", "SELECT 'a\\\"b'", "SELECT 'a\\\\b'", "SELECT 'a\\/b'",
            "SELECT 'a\\nb'", "SELECT 'a\\u0041b'", "SELECT 'a\\u004'", "SELECT 'a\\xb'", "SELECT 'a\nb'", "SELECT 'a\tb'",
            "SELECT 'é'", "SELECT 'unterminated", "SELECT \"'\"", "SELECT '\"'", "SELECT {\"a\": 1, 'b': 2}", "SELECT {}",
            "SELECT {1: 2}", "SELECT {\"a\": 1,}", "SELECT []", "SELECT [1, 2]", "SELECT [1,]", "SELECT c['a'] FROM c",

            // Operators.
            "SELECT 1 || 2", "SELECT 1 | 2", "SELECT 1 | | 2", "SELECT 1 ||| 2", "SELECT 1 & 2", "SELECT 1 && 2", "SELECT 1 ^ 2",
            "SELECT 1 >= 2", "SELECT 1 > = 2", "SELECT 1 <= 2", "SELECT 1 < 2", "SELECT 1 != 2", "SELECT 1 == 2", "SELECT 1 <> 2",
            "SELECT 1 = 2 = 3", "SELECT 1 + 2 * 3 - 4 / 5 % 6", "SELECT 1 | 2 ^ 3 & 4 || 5", "SELECT 1 = 2 < 3 + 4",
            "SELECT a ? b : c", "SELECT a ? b : c ? d : e", "SELECT a ? b ? c : d : e", "SELECT a ?? b ?? c",
            "SELECT a ?? b ? c : d", "SELECT a ? b : c ?? d", "SELECT a ?? b ? c : d ?? e", "SELECT a ??? b", "SELECT a ? ? b : c",
            "SELECT a ? b", "SELECT (a ? b : c) ? d : e", "SELECT a AND b OR c AND d", "SELECT a OR b AND c", "SELECT NOT a AND b",
            "SELECT a AND b ? c : d", "SELECT a ?? b AND c",

            // IN, LIKE, BETWEEN.
            "SELECT * FROM c WHERE c.a IN (1, 2)", "SELECT * FROM c WHERE c.a NOT IN (1)", "SELECT * FROM c WHERE c.a IN ()",
            "SELECT * FROM c WHERE c.a IN 1", "SELECT * FROM c WHERE c.a LIKE 'a%'", "SELECT * FROM c WHERE c.a NOT LIKE 'a%' ESCAPE '!'",
            "SELECT * FROM c WHERE c.a LIKE 'a' ESCAPE 1", "SELECT * FROM c WHERE c.a BETWEEN 1 AND 2",
            "SELECT * FROM c WHERE c.a NOT BETWEEN 1 AND 2", "SELECT * FROM c WHERE c.a BETWEEN 1 AND 2 AND c.b",
            "SELECT * FROM c WHERE c.b AND c.a BETWEEN 1 AND 2", "SELECT * FROM c WHERE (c.a BETWEEN 1 AND 2) AND c.b",
            "SELECT * FROM c WHERE c.a BETWEEN 1 AND 2 OR c.b", "SELECT * FROM c WHERE c.a BETWEEN 1 AND 2 ?? 3",
            "SELECT * FROM c WHERE c.x ?? c.a BETWEEN 1 AND 2", "SELECT * FROM c WHERE c.x ? c.a BETWEEN 1 AND 2 : 3",
            "SELECT * FROM c WHERE c.x ? 1 : c.a BETWEEN 1 AND 2", "SELECT * FROM c WHERE c.a BETWEEN 1 + 1 AND 2 * 2",
            "SELECT * FROM c WHERE c.a IN (1) AND c.b LIKE 'x' OR c.c NOT IN (2)", "SELECT * FROM c WHERE c.a NOT c.b",
            "SELECT * FROM c WHERE NOT c.a IN (1)", "SELECT * FROM c WHERE c.a = 1 IN (true)", "SELECT * FROM c WHERE c.a IN (1) IN (2)",
            "SELECT * FROM c WHERE c.a LIKE c.b LIKE c.c", "SELECT * FROM c WHERE c.a BETWEEN 1 AND 2 BETWEEN 3 AND 4",

            // FROM.
            "SELECT * FROM c", "SELECT * FROM c AS d", "SELECT * FROM c d", "SELECT * FROM c AS", "SELECT * FROM c.a.b[1]['x']",
            "SELECT * FROM c[-1]", "SELECT * FROM c[1.5]", "SELECT * FROM c[a]", "SELECT * FROM d IN c.items",
            "SELECT * FROM c JOIN d IN c.items JOIN e IN d.x", "SELECT * FROM (SELECT VALUE 1) AS x", "SELECT * FROM x IN (SELECT VALUE 1)",
            "SELECT * FROM c JOIN (SELECT VALUE 1) x", "SELECT * FROM c IN", "SELECT * FROM c JOIN", "SELECT * FROM",

            // Clauses.
            "SELECT * FROM c GROUP BY c.a, c.b", "SELECT * FROM c ORDER BY c.a DESC, c.b", "SELECT * FROM c ORDER BY c.a ASC",
            "SELECT * FROM c ORDER BY RANK f(c)", "SELECT * FROM c ORDER BY RANK f(c) DESC, g(c)", "SELECT * FROM c ORDER BY RANK c.a",
            "SELECT * FROM c ORDER BY RANK udf.f(c)", "SELECT * FROM c ORDER BY RANK left(c)", "SELECT * FROM c WHERE true GROUP BY c.a ORDER BY c.a OFFSET 0 LIMIT 1",
            "SELECT * FROM c ORDER BY c.a GROUP BY c.a", "SELECT * WHERE true", "SELECT VALUE COUNT(1) FROM c", "SELECT c.a AS x, c.b y FROM c",
            "SELECT *, 1", "SELECT * 1", "SELECT VALUE", "SELECT", "", " ", "SELECT 1 $", "SELECT $", "SELECT # FROM c", "SELECT 1;",
            "SELECT EXISTS(SELECT 1)", "SELECT ARRAY(SELECT VALUE 1)", "SELECT (SELECT VALUE 1)", "SELECT (1)", "SELECT ((1))",
            "SELECT f(1).a", "SELECT f(1)[0]", "SELECT (1).a", "SELECT 'a'.b", "SELECT c.a[0].b[\"x\"] FROM c", "SELECT c . a FROM c",
            "SELECT f()", "SELECT f(,)", "SELECT f(1,)", "SELECT f (1)",
        };

        [TestMethod]
        public void BaselineQueries()
        {
            Compare(LoadBaselineQueries());
        }

        [TestMethod]
        public void EdgeCaseQueries()
        {
            Compare(EdgeCases);
        }

        [TestMethod]
        public void MutatedBaselineQueries()
        {
            List<string> queries = new List<string>();
            foreach (string query in LoadBaselineQueries().Concat(EdgeCases))
            {
                for (int i = 0; i < query.Length; i++)
                {
                    // Every prefix and every query with one char removed.
                    queries.Add(query.Substring(0, i));
                    queries.Add(query.Remove(i, 1));
                }
            }

            Compare(queries.Distinct());
        }

        [TestMethod]
        public void RandomTokenSequences()
        {
            string[] tokens = new string[]
            {
                "SELECT", "select", "VALUE", "DISTINCT", "TOP", "FROM", "WHERE", "GROUP", "BY", "ORDER", "RANK", "ASC", "DESC",
                "OFFSET", "LIMIT", "AS", "IN", "JOIN", "AND", "OR", "NOT", "BETWEEN", "LIKE", "ESCAPE", "EXISTS", "ARRAY", "ALL",
                "FIRST", "LAST", "LEFT", "RIGHT", "udf", "true", "false", "null", "undefined", "TRUE", "c", "d", "_x", "f", "@p",
                "@", "1", "-1", "+2", "1.5", ".5", "1e3", "-", "+", "*", "/", "%", "<", ">", "<=", ">=", "=", "!=", "&", "|", "^",
                "||", "~", "?", "??", ":", ",", ".", "(", ")", "[", "]", "{", "}", "'s'", "\"t\"", "$",
            };

            Random random = new Random(20261031);
            List<string> queries = new List<string>();
            for (int i = 0; i < 20000; i++)
            {
                StringBuilder builder = new StringBuilder(random.Next(2) == 0 ? "SELECT " : string.Empty);
                int count = random.Next(1, 14);
                for (int j = 0; j < count; j++)
                {
                    builder.Append(tokens[random.Next(tokens.Length)]);
                    builder.Append(random.Next(4) == 0 ? string.Empty : " ");
                }

                queries.Add(builder.ToString());
            }

            Compare(queries);
        }

        [TestMethod]
        public void RandomValidQueries()
        {
            Random random = new Random(42);
            List<string> queries = new List<string>();
            for (int i = 0; i < 5000; i++)
            {
                queries.Add(RandomQuery(random, depth: 0));
            }

            int succeeded = Compare(queries);
            Assert.IsTrue(succeeded > queries.Count / 2, $"Only {succeeded} of {queries.Count} generated queries are valid.");
        }

        private static string RandomQuery(Random random, int depth)
        {
            StringBuilder builder = new StringBuilder("SELECT ");
            if (random.Next(4) == 0)
            {
                builder.Append("DISTINCT ");
            }

            if (random.Next(4) == 0)
            {
                builder.Append(random.Next(2) == 0 ? "TOP 5 " : "TOP @top ");
            }

            switch (random.Next(3))
            {
                case 0:
                    builder.Append('*');
                    break;
                case 1:
                    builder.Append("VALUE ").Append(RandomExpression(random, depth + 1));
                    break;
                default:
                    builder.Append(RandomExpression(random, depth + 1)).Append(random.Next(2) == 0 ? " AS a1" : string.Empty)
                        .Append(", ").Append(RandomExpression(random, depth + 1));
                    break;
            }

            if (random.Next(3) != 0)
            {
                builder.Append(" FROM c");
                if (random.Next(3) == 0)
                {
                    builder.Append(" JOIN t IN c.tags JOIN (SELECT VALUE 1) AS s");
                }
            }

            if (random.Next(2) == 0)
            {
                builder.Append(" WHERE ").Append(RandomExpression(random, depth + 1));
            }

            if (random.Next(5) == 0)
            {
                builder.Append(" GROUP BY ").Append(RandomExpression(random, depth + 1));
            }

            if (random.Next(5) == 0)
            {
                builder.Append(" ORDER BY ").Append(RandomExpression(random, depth + 1)).Append(random.Next(2) == 0 ? " DESC" : string.Empty);
            }

            if (random.Next(6) == 0)
            {
                builder.Append(" OFFSET 1 LIMIT @limit");
            }

            return builder.ToString();
        }

        private static string RandomExpression(Random random, int depth)
        {
            string[] binary = new string[] { "+", "-", "*", "/", "%", "<", ">", "<=", ">=", "=", "!=", "&", "|", "^", "||", "AND", "OR", "??" };
            if (depth > 4)
            {
                return RandomAtom(random);
            }

            switch (random.Next(12))
            {
                case 0:
                case 1:
                case 2:
                    return RandomAtom(random);
                case 3:
                case 4:
                case 5:
                    return $"{RandomExpression(random, depth + 1)} {binary[random.Next(binary.Length)]} {RandomExpression(random, depth + 1)}";
                case 6:
                    return $"{RandomExpression(random, depth + 1)} ? {RandomExpression(random, depth + 1)} : {RandomExpression(random, depth + 1)}";
                case 7:
                    return $"{new[] { "-", "+", "~", "NOT " }[random.Next(4)]}{RandomExpression(random, depth + 1)}";
                case 8:
                    return $"({RandomExpression(random, depth + 1)})";
                case 9:
                    return random.Next(3) switch
                    {
                        0 => $"{RandomExpression(random, depth + 1)} {(random.Next(2) == 0 ? "NOT " : string.Empty)}IN ({RandomAtom(random)}, {RandomAtom(random)})",
                        1 => $"{RandomExpression(random, depth + 1)} {(random.Next(2) == 0 ? "NOT " : string.Empty)}LIKE 'a%' ESCAPE '!'",
                        _ => $"{RandomExpression(random, depth + 1)} {(random.Next(2) == 0 ? "NOT " : string.Empty)}BETWEEN {RandomExpression(random, depth + 1)} AND {RandomExpression(random, depth + 1)}",
                    };
                case 10:
                    return $"{new[] { "ABS", "udf.f", "LEFT", "ARRAY_LENGTH" }[random.Next(4)]}({RandomExpression(random, depth + 1)}, {RandomAtom(random)})";
                default:
                    return random.Next(4) switch
                    {
                        0 => $"EXISTS({RandomQuery(random, depth + 2)})",
                        1 => $"[{RandomExpression(random, depth + 1)}, {RandomAtom(random)}]",
                        2 => $"{{\"k\": {RandomExpression(random, depth + 1)}}}",
                        _ => $"({RandomQuery(random, depth + 2)})",
                    };
            }
        }

        private static string RandomAtom(Random random)
        {
            string[] atoms = new string[] { "c", "c.a", "c.a.b", "c[\"x\"]", "c.items[0]", "1", "-2", "3.5", ".5e2", "'s'", "\"t\"", "true", "null", "undefined", "@p", "ALL", "c.first" };
            return atoms[random.Next(atoms.Length)];
        }

        private static IEnumerable<string> LoadBaselineQueries()
        {
            string directory = Path.Combine("BaselineTest", "TestBaseline");
            List<string> queries = Directory.GetFiles(directory, "*SqlParserBaselineTests.*.xml")
                .SelectMany(file => XDocument.Load(file).Descendants("Query"))
                .Select(element => element.Value)
                .Distinct()
                .ToList();
            Assert.IsTrue(queries.Count > 200, $"Expected the parser baselines, found {queries.Count} queries.");
            return queries;
        }

        private static int Compare(IEnumerable<string> queries)
        {
            int count = 0;
            int succeeded = 0;
            StringBuilder errors = new StringBuilder();
            foreach (string query in queries)
            {
                count++;
                TryCatch<SqlQuery> antlr = AntlrSqlQueryParser.Monadic.Parse(query);
                TryCatch<SqlQuery> parlot = SqlQueryParser.Monadic.Parse(query);

                if (antlr.Succeeded != parlot.Succeeded)
                {
                    errors.AppendLine($"[{Escape(query)}] ANTLR: {Describe(antlr)} Parlot: {Describe(parlot)}");
                }
                else if (antlr.Succeeded
                    && (antlr.Result.ToString() != parlot.Result.ToString() || !antlr.Result.Equals(parlot.Result)))
                {
                    errors.AppendLine($"[{Escape(query)}] ANTLR: {antlr.Result} Parlot: {parlot.Result}");
                }

                if (antlr.Succeeded)
                {
                    succeeded++;
                }
            }

            Assert.AreEqual(string.Empty, errors.ToString(), $"Differences among {count} queries");
            return succeeded;
        }

        private static string Describe(TryCatch<SqlQuery> result)
        {
            if (result.Succeeded)
            {
                return result.Result.ToString();
            }

            Exception exception = result.Exception;
            while (exception.InnerException != null)
            {
                exception = exception.InnerException;
            }

            return $"<{exception.GetType().Name}: {exception.Message}>";
        }

        private static string Escape(string query)
        {
            StringBuilder builder = new StringBuilder();
            foreach (char c in query)
            {
                builder.Append(c < ' ' || c > '~' ? $"\\u{(int)c:x4}" : c.ToString());
            }

            return builder.ToString();
        }
    }
}
