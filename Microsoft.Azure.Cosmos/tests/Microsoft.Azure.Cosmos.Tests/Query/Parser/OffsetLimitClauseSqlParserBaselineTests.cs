//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
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

        [TestMethod]
        [DataRow("SELECT * FROM c OFFSET 5555555555555555555555555555555555555555 LIMIT 10", DisplayName = "OffsetOverflowsInt64")]
        [DataRow("SELECT * FROM c OFFSET 10 LIMIT 5555555555555555555555555555555555555555", DisplayName = "LimitOverflowsInt64")]
        [DataRow("SELECT * FROM c OFFSET 1.5 LIMIT 10", DisplayName = "OffsetNonInteger")]
        [DataRow("SELECT * FROM c OFFSET 10 LIMIT 1.5", DisplayName = "LimitNonInteger")]
        [DataRow(
            "SELECT c.type, COUNT(1) AS cnt FROM c JOIN t INBETWEEN WHERE ARRAy_CONTAINS(c.tags, \"x\") GROUP BY c.type ORDER BY c.type OFFSET 5555555555555555555555555555555555555555 LIMIT 10",
            DisplayName = "FuzzerRepro")]
        public void Parse_InvalidCountLiteral_ReturnsFailed(string query)
        {
            TryCatch<SqlQuery> result = SqlQueryParser.Monadic.Parse(query);

            Assert.IsTrue(result.Failed);
            Assert.IsFalse(SqlQueryParser.TryParse(query, out SqlQuery _));
        }

        [TestMethod]
        [DataRow("SELECT * FROM c OFFSET 9223372036854775807 LIMIT 10")]
        public void Parse_ValidCountLiteral_Succeeds(string query)
        {
            Assert.IsTrue(SqlQueryParser.Monadic.Parse(query).Succeeded);
        }

        public static SqlParserBaselineTestInput CreateInput(string description, string offsetLimitClause)
        {
            return new SqlParserBaselineTestInput(description, $"SELECT * {offsetLimitClause}");
        }
    }
}