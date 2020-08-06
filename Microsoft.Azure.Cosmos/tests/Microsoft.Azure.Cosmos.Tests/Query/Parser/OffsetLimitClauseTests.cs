namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OffsetLimitClauseTests : ParserTests
    {
        [TestMethod]
        public void OffsetLimit()
        {
            OffsetLimitClauseTests.ValidateOffsetLimit("OFFSET 10 LIMIT 10");

            OffsetLimitClauseTests.InvalidateOffsetLimit("OFFSET 'asdf' LIMIT 10");
            OffsetLimitClauseTests.InvalidateOffsetLimit("OFFSET 10 ");
            OffsetLimitClauseTests.InvalidateOffsetLimit("LIMIT 10 ");
        }

        private static void ValidateOffsetLimit(string offsetLimitClause)
        {
            string query = $"SELECT * {offsetLimitClause}";
            ParserTests.Validate(query);
        }

        private static void InvalidateOffsetLimit(string offsetLimitClause)
        {
            string query = $"SELECT * {offsetLimitClause}";
            ParserTests.Invalidate(query);
        }
    }
}
