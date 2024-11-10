//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class OrderByClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void SingleOrderBy()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", orderByClause: "ORDER BY 1"),
                CreateInput(description: "Ascending", orderByClause: "ORDER BY 1 asc"),
                CreateInput(description: "Multi Item Array", orderByClause: "ORDER BY 1 DESC"),
                CreateInput(description: "Case Insensitive", orderByClause: "OrDeR By 1 DeSc"),

                // Negative
                CreateInput(description: "No spaces", orderByClause: "ORDERBY 1"),
                CreateInput(description: "Wrong Keyword", orderByClause: "ORER BY 1"),
            };

            this.ExecuteTestSuite(inputs);
        }

        [TestMethod]
        public void MultiOrderBy()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", orderByClause: "ORDER BY 1, 2, 3"),
                CreateInput(description: "Only one sort order", orderByClause: "ORDER BY 1, 2 DESC, 3"),
                CreateInput(description: "All sort order", orderByClause: "ORDER BY 1 ASC, 2 DESC, 3 ASC"),

                // Negative
                CreateInput(description: "Trailing comma", orderByClause: "ORDER BY 1 ASC,"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string orderByClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {orderByClause}");
        }
    }
}