//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class OffsetLimitClauseSqlParserBaselineTests : SqlParserBaselineTests
    {
        [TestMethod]
        public void Tests()
        {
            List<SqlParserBaselineTestInput> inputs = new List<SqlParserBaselineTestInput>()
            {
                // Positive
                CreateInput(description: "Basic", offsetLimitClause: "OFFSET 10 LIMIT 10"),
                CreateInput(description: "Parameters", offsetLimitClause: "OFFSET @OFFSETCOUNT LIMIT @LIMITCOUNT"),

                // Negative
                CreateInput(description: "Non integer or paramter count", offsetLimitClause: "OFFSET 'asdf' LIMIT 10"),
                CreateInput(description: "Offset without limit", offsetLimitClause: "OFFSET 10"),
                CreateInput(description: "Limit without offset", offsetLimitClause: "LIMIT 10"),
            };

            this.ExecuteTestSuite(inputs);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string offsetLimitClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {offsetLimitClause}");
        }
    }
}