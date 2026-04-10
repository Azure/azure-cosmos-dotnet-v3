//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class GroupByClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", groupByClause: "GROUP BY 1"),
                CreateInput(description: "Case Insensitive", groupByClause: "GrOuP By 1"),
                CreateInput(description: "multi group by", groupByClause: "GROUP BY 1, 2, 3"),

                // Negative
                CreateInput(description: "missing group by expression", groupByClause: "GROUP BY "),
                CreateInput(description: "missing space", groupByClause: "GROUPBY 1"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string groupByClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {groupByClause}");
        }
    }
}