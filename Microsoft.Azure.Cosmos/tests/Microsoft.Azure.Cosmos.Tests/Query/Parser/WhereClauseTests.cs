namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class WhereClauseTests : ParserTests
    {
        [TestMethod]
        public void WhereClause()
        {
            WhereClauseTests.ValidateWhere("WHERE true");
            WhereClauseTests.InvalidateWhere("WHERE true, true");
        }

        private static void ValidateWhere(string whereClause)
        {
            string query = $"SELECT * {whereClause}";
            WhereClauseTests.Validate(query);
        }

        private static void InvalidateWhere(string whereClause)
        {
            string query = $"SELECT * {whereClause}";
            WhereClauseTests.Invalidate(query);
        }
    }
}
