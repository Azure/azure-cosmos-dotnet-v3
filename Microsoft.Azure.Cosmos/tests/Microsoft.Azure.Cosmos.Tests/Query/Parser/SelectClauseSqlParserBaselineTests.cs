//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class SelectClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Select Star", selectClause: "SELECT *"),
                CreateInput(description: "Case Insensitive", selectClause: "SeLeCt *"),
                CreateInput(description: "Select List", selectClause: "SELECT 1, 2, 3"),
                CreateInput(description: "Select List with aliases", selectClause: "SELECT 1 AS asdf, 2, 3 AS asdf2"),
                CreateInput(description: "Select Value", selectClause: "SELECT VALUE 1"),
                CreateInput(description: "DISTINCT", selectClause: "SELECT DISTINCT *"),
                CreateInput(description: "TOP", selectClause: "SELECT TOP 5 *"),
                CreateInput(description: "TOP with parameters", selectClause: "SELECT TOP @TOPCOUNT *"),

                // Negative
                CreateInput(description: "No Selection", selectClause: "SELECT"),
                CreateInput(description: "Wrong keyword", selectClause: "Selec *"),
                CreateInput(description: "Trailing comma", selectClause: "SELECT 1,"),
                CreateInput(description: "Select Value more than 1 expression", selectClause: "SELECT VALUE 1, 2"),
                CreateInput(description: "Select Value no spaces", selectClause: "SELECTVALUE 1"),
                CreateInput(description: "TOP non number", selectClause: "SELECT TOP 'asdf' *"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string selectClause)
        {
            return new SqlParserBaselineTestInput(description, selectClause);
        }
    }
}