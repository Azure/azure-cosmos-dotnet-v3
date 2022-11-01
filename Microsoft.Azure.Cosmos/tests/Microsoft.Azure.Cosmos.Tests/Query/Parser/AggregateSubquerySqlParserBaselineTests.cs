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
                    query: "SELECT 1 as ALL"),
                CreateInput(
                    description: "ALL in an AliasedCollectionExpression as an alias",
                    query: "SELECT * " +
                           "FROM (SELECT VALUE 1) as ALL"),
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
                    query: "SELECT ALL(1, 2) as ALL " +
                           "FROM ALL IN (SELECT ALL.ALL) " +
                           "WHERE ALL( " +
                           "    SELECT ALL " +
                           "    FROM (SELECT udf.ALL(1, 2)) as ALL " +
                           "    WHERE ALL( SELECT VALUE 1) " +
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
