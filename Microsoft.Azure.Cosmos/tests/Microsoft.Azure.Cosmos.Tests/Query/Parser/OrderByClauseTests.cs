namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OrderByClauseTests : ParserTests
    {
        [TestMethod]
        public void SingleOrderBy()
        {
            //OrderByClauseTests.ValidateOrderBy("ORDER BY 1");
            //OrderByClauseTests.ValidateOrderBy("ORDER BY 1 asc");
            //OrderByClauseTests.ValidateOrderBy("ORDER BY 1 DESC");
            OrderByClauseTests.InvalidateOrderBy("ORDERBY 1");
        }

        [TestMethod]
        public void MultiOrderBy()
        {
            OrderByClauseTests.ValidateOrderBy("ORDER BY 1, 2, 3");
            OrderByClauseTests.ValidateOrderBy("ORDER BY 1, 2 DESC, 3");
            OrderByClauseTests.ValidateOrderBy("ORDER BY 1 ASC, 2 DESC, 3 ASC");
            OrderByClauseTests.InvalidateOrderBy("ORDER BY 1 ASC,");
        }

        private static void ValidateOrderBy(string orderByClause)
        {
            string query = $"SELECT * {orderByClause}";
            ParserTests.Validate(query);
        }

        private static void InvalidateOrderBy(string orderByClause)
        {
            string query = $"SELECT * {orderByClause}";
            ParserTests.Invalidate(query);
        }
    }
}
