namespace Microsoft.Azure.Cosmos.Tests.Query.Parser
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class SelectClauseTests : ParserTests
    {
        [TestMethod]
        public void SelectStar()
        {
            ParserTests.Validate("SELECT *");
            ParserTests.Invalidate("SELECT");
        }

        [TestMethod]
        public void SelectList()
        {
            ParserTests.Validate("SELECT 1, 2, 3");
            ParserTests.Validate("SELECT 1 AS asdf, 2, 3 AS asdf2");
            ParserTests.Invalidate("SELECT 1,");
        }

        [TestMethod]
        public void SelectValue()
        {
            ParserTests.Validate("SELECT VALUE 1");
            ParserTests.Invalidate("SELECT VALUE 1, 2");
            ParserTests.Invalidate("SELECTVALUE 1");
        }

        [TestMethod]
        public void Distinct()
        {
            ParserTests.Validate("SELECT DISTINCT *");
        }

        [TestMethod]
        public void Top()
        {
            ParserTests.Validate("SELECT TOP 5 *");
            ParserTests.Invalidate("SELECT TOP 'asdf' *");
        }
    }
}
