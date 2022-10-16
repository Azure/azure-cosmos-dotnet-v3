//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class AggregateAllSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                CreateInput(description: "ALL in an SqlSelectItem as an alias", scalarExpression: "SELECT 1 as ALL"),
                CreateInput(description: "ALL in an AliasedCollectionExpression as an alias", scalarExpression:
               "SELECT * " +
               "FROM (SELECT VALUE 1) as ALL"),
                CreateInput(description: "ALL in an ArrayIteratorCollectionExpression", scalarExpression:
               "SELECT * " +
               "FROM ALL IN (SELECT VALUE 1)"),
                CreateInput(description: "ALL in an InputPathCollection and IdentifierPathExpression", scalarExpression:
               "SELECT * " +
               "FROM ALL.ALL"),
                CreateInput(description: "ALL in a PropertyRefScalarExpression", scalarExpression: "SELECT ALL"),
                CreateInput(description: "ALL in a PropertyRefScalarExpression as child", scalarExpression: "SELECT c.ALL"),
                CreateInput(description: "ALL in a PropertyRefScalarExpression as parent and child", scalarExpression: "SELECT ALL.ALL"),
                CreateInput(description: "ALL in a function call", scalarExpression: "SELECT ALL( 1, 2)"),
                CreateInput(description: "ALL in a UDF function call", scalarExpression: "SELECT udf.ALL( 1, 2)"),
            };
            
            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string scalarExpression)
        {
            return new SqlParserBaselineTestInput(description, scalarExpression);
        }
    }
}
