//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class AggregateSubquerySqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void All()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                CreateInput(
                    description: "ALL in an SqlSelectItem as an alias",
                    query: "SELECT 1 AS ALL"),
                CreateInput(
                    description: "ALL in an AliasedCollectionExpression as an alias",
                    query: "SELECT * " +
                           "FROM (SELECT VALUE 1) AS ALL"),
                CreateInput(
                    description: "ALL in an ArrayIteratorCollectionExpression",
                    query: "SELECT * " +
                           "FROM ALL IN (SELECT VALUE 1)"),
                CreateInput(
                    description: "ALL in an InputPathCollection and IdentifierPathExpression",
                    query: "SELECT * " +
                           "FROM ALL.ALL"),
                CreateInput(
                    description: "ALL in a PropertyRefScalarExpression",
                    query: "SELECT ALL"),
                CreateInput(
                    description: "ALL in a PropertyRefScalarExpression as child",
                    query: "SELECT c.ALL"),
                CreateInput(
                    description: "ALL in a PropertyRefScalarExpression as parent and child",
                    query: "SELECT ALL.ALL"),
                CreateInput(
                    description: "ALL in a function call",
                    query: "SELECT ALL(1, 2)"),
                CreateInput(
                    description: "ALL in a UDF function call",
                    query: "SELECT udf.ALL(1, 2)"),
                CreateInput(
                    description: "ALL in every possible grammar rule at the same time",
                    query: "SELECT ALL(1, 2) AS ALL " +
                           "FROM ALL IN (SELECT ALL.ALL) " +
                           "WHERE ALL( " +
                           "    SELECT ALL " +
                           "    FROM (SELECT udf.ALL(1, 2)) AS ALL " +
                           "    WHERE ALL( SELECT VALUE 1) " +
                           ")")
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void First()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                CreateInput(
                    description: "FIRST in an SqlSelectItem as an alias",
                    query: "SELECT 1 AS FIRST"),
                CreateInput(
                    description: "FIRST in an AliasedCollectionExpression as an alias",
                    query: "SELECT * " +
                           "FROM (SELECT VALUE 1) AS FIRST"),
                CreateInput(
                    description: "FIRST in an ArrayIteratorCollectionExpression",
                    query: "SELECT * " +
                           "FROM FIRST IN (SELECT VALUE 1)"),
                CreateInput(
                    description: "FIRST in an InputPathCollection and IdentifierPathExpression",
                    query: "SELECT * " +
                           "FROM FIRST.FIRST"),
                CreateInput(
                    description: "FIRST in a PropertyRefScalarExpression",
                    query: "SELECT FIRST"),
                CreateInput(
                    description: "FIRST in a PropertyRefScalarExpression as child",
                    query: "SELECT c.FIRST"),
                CreateInput(
                    description: "FIRST in a PropertyRefScalarExpression as parent and child",
                    query: "SELECT FIRST.FIRST"),
                CreateInput(
                    description: "FIRST in a function cFIRST",
                    query: "SELECT FIRST(1, 2)"),
                CreateInput(
                    description: "FIRST in a UDF function cFIRST",
                    query: "SELECT udf.FIRST(1, 2)"),
                CreateInput(
                    description: "FIRST in every possible grammar rule at the same time",
                    query: "SELECT FIRST(1, 2) AS FIRST " +
                           "FROM FIRST IN (SELECT FIRST.FIRST) " +
                           "WHERE FIRST( " +
                           "    SELECT FIRST " +
                           "    FROM (SELECT udf.FIRST(1, 2)) AS FIRST " +
                           "    WHERE FIRST( SELECT VALUE 1) " +
                           ")")
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void Last()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                CreateInput(
                    description: "LAST in an SqlSelectItem as an alias",
                    query: "SELECT 1 AS LAST"),
                CreateInput(
                    description: "LAST in an AliasedCollectionExpression as an alias",
                    query: "SELECT * " +
                           "FROM (SELECT VALUE 1) AS LAST"),
                CreateInput(
                    description: "LAST in an ArrayIteratorCollectionExpression",
                    query: "SELECT * " +
                           "FROM LAST IN (SELECT VALUE 1)"),
                CreateInput(
                    description: "LAST in an InputPathCollection and IdentifierPathExpression",
                    query: "SELECT * " +
                           "FROM LAST.LAST"),
                CreateInput(
                    description: "LAST in a PropertyRefScalarExpression",
                    query: "SELECT LAST"),
                CreateInput(
                    description: "LAST in a PropertyRefScalarExpression as child",
                    query: "SELECT c.LAST"),
                CreateInput(
                    description: "LAST in a PropertyRefScalarExpression as parent and child",
                    query: "SELECT LAST.LAST"),
                CreateInput(
                    description: "LAST in a function cLAST",
                    query: "SELECT LAST(1, 2)"),
                CreateInput(
                    description: "LAST in a UDF function cLAST",
                    query: "SELECT udf.LAST(1, 2)"),
                CreateInput(
                    description: "LAST in every possible grammar rule at the same time",
                    query: "SELECT LAST(1, 2) AS LAST " +
                           "FROM LAST IN (SELECT LAST.LAST) " +
                           "WHERE LAST( " +
                           "    SELECT LAST " +
                           "    FROM (SELECT udf.LAST(1, 2)) AS LAST " +
                           "    WHERE LAST( SELECT VALUE 1) " +
                           ")")
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string query)
        {
            return new SqlParserBaselineTestInput(description, query);
        }
    }
}