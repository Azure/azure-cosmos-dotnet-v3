//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class LikeClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", likeClause: "LIKE '$a'"),
                CreateInput(description: "With ESCAPE", likeClause: "LIKE 'a!%' ESCAPE '!'"),
                CreateInput(description: "With NOT", likeClause: "NOT LIKE 'a!%' ESCAPE '!'"),

                // Negative
                CreateInput(description: "Missing LIKE Keyword 1", likeClause: "ESCAPE '1'"),
                CreateInput(description: "Missing LIKE Keyword 2", likeClause: "NOT 'a'"),
                CreateInput(description: "Missing ESCAPE Keyword", likeClause: "LIKE \"a!%\" \"!\""),
                CreateInput(description: "Double LIKE", likeClause: "(LIKE 'a') LIKE 'b'"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string likeClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT c.age {likeClause}");
        }
    }
}