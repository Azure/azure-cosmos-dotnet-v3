//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.Azure.Cosmos.Query.Core.Monads;
    using Microsoft.Azure.Cosmos.Query.Core.Parser;
    using Microsoft.Azure.Cosmos.SqlObjects;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SqlQueryParserMonadicTests
    {
        [TestMethod]
        [DataRow("SELECT * FROM c OFFSET 5555555555555555555555555555555555555555 LIMIT 10", DisplayName = "OffsetOverflowsInt64")]
        [DataRow("SELECT * FROM c OFFSET 10 LIMIT 5555555555555555555555555555555555555555", DisplayName = "LimitOverflowsInt64")]
        [DataRow("SELECT TOP 5555555555555555555555555555555555555555 * FROM c", DisplayName = "TopOverflowsInt64")]
        [DataRow("SELECT * FROM c OFFSET 1.5 LIMIT 10", DisplayName = "OffsetNonInteger")]
        [DataRow("SELECT * FROM c OFFSET 10 LIMIT 1.5", DisplayName = "LimitNonInteger")]
        [DataRow("SELECT TOP 1.5 * FROM c", DisplayName = "TopNonInteger")]
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
        [DataRow("SELECT TOP 10 * FROM c")]
        public void Parse_ValidCountLiteral_Succeeds(string query)
        {
            Assert.IsTrue(SqlQueryParser.Monadic.Parse(query).Succeeded);
        }
    }
}
