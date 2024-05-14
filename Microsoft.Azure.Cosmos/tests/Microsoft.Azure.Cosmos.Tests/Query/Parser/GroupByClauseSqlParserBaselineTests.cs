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

                CreateInput(description: "single group by with alias", groupByClause: "FROM r GROUP BY r.id AS GroupByKey"),
                CreateInput(description: "single group by with alias no AS", groupByClause: "FROM r GROUP BY r.id GroupByKey"),
                CreateInput(description: "multi group by with alias", groupByClause: "FROM r GROUP BY r.id AS GroupByKey, r.name AS Name"),
                CreateInput(description: "multi group by with alias no AS", groupByClause: "FROM r GROUP BY r.id GroupByKey, r.name Name"),

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
