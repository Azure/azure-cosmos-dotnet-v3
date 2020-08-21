namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class GroupByClauseTests : ParserTests
    {
        [TestMethod]
        public void SingleGroupBy()
        {
            GroupByClauseTests.ValidateGroupBy("GROUP BY 1");
            GroupByClauseTests.InvalidateGroupBy("GROUP BY ");
            GroupByClauseTests.InvalidateGroupBy("GROUPBY 1");
        }

        [TestMethod]
        public void MultiGroupBy()
        {
            GroupByClauseTests.ValidateGroupBy("GROUP BY 1, 2, 3");
            GroupByClauseTests.ValidateGroupBy("GROUP BY 1, 2");
            GroupByClauseTests.InvalidateGroupBy("GROUP BY 1,");
        }

        private static void ValidateGroupBy(string groupByClause)
        {
            string query = $"SELECT * {groupByClause}";
            ParserTests.Validate(query);
        }

        private static void InvalidateGroupBy(string groupByClause)
        {
            string query = $"SELECT * {groupByClause}";
            ParserTests.Invalidate(query);
        }
    }
}
