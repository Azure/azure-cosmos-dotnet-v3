namespace Microsoft.Azure.Cosmos.Tests.Linq
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosLinqAdvancedFilteringTest
    {
        [TestMethod]
        public void TestArrayContains()
        {
            // Should translate to ARRAY_CONTAINS with array being on document
            Expression<Func<TestDocument, bool>> expr = a => a.Statuses.Contains("Active");
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("ARRAY_CONTAINS(a[\"Statuses\"], \"Active\")", sql);

            // Should translate to IN with array being in variable
            List<string> names = new List<string>() { "Test", "John", "Smith" };
            expr = a => names.Contains(a.Name);
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(a[\"Name\"] IN (\"Test\", \"John\", \"Smith\"))", sql);
        }

        [TestMethod]
        public void TestDictionaryContains()
        {
            // Should be ["Item"] = true
            Expression<Func<TestDocument, bool>> expr = a => ((bool)a.Metadata["Item"]) == true;
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(a[\"Metadata\"][\"Item\"] = true)", sql);

            // Should be ["Item"].["Key"] = "Other Value"
            expr = a => ((string)((Dictionary<string, object>)a.Metadata["Item"])["Key"]) == "Other Value";
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("(a[\"Metadata\"][\"Item\"][\"Key\"] = \"Other Value\")", sql);
        }

        [TestMethod]
        public void TestStringSearch()
        {
            // Contains / case insensitive
            Expression<Func<TestDocument, bool>> expr = a => a.Name.Contains("Test", StringComparison.OrdinalIgnoreCase);
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("CONTAINS(a[\"Name\"], \"Test\", true)", sql);

            // Contains / case sensitive
            expr = a => a.Name.Contains("Test", StringComparison.Ordinal);
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("CONTAINS(a[\"Name\"], \"Test\", false)", sql);

            // EndsWith
            expr = a => a.Name.EndsWith("Test", StringComparison.OrdinalIgnoreCase);
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("ENDSWITH(a[\"Name\"], \"Test\", true)", sql);

            // StringEquals 
            expr = a => a.Name.Equals("Test", StringComparison.OrdinalIgnoreCase);
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("StringEquals(a[\"Name\"], \"Test\", true)", sql);

            // StartsWith
            expr = a => a.Name.StartsWith("Test", StringComparison.OrdinalIgnoreCase);
            sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("STARTSWITH(a[\"Name\"], \"Test\", true)", sql);
        }

        [TestMethod]
        public void TestLike()
        {
            // Should translate to LIKE
            Expression<Func<TestDocument, bool>> expr = a => a.Name.Like("Test%", "!");
            string sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"Name\"] LIKE \"Test%\" ESCAPE \"!\")", sql);

            // Should translate to NOT LIKE
            expr = a => a.Name.NotLike("Test%");
            sql = SqlTranslator.TranslateExpression(expr.Body);
            Assert.AreEqual("(a[\"Name\"] NOT  LIKE \"Test%\")", sql);
        }

        [TestMethod]
        public void TestTypeCasting()
        {
            Expression<Func<BaseDocument, bool>> expr = a => ((TestDocument)a).Statuses.Contains("Active");
            string sql = SqlTranslator.TranslateExpression(expr.Body);

            Assert.AreEqual("ARRAY_CONTAINS(a[\"Statuses\"], \"Active\")", sql);
        }

        abstract class BaseDocument
        {
            public abstract string Kind { get; }
            public virtual string Name { get; set; } = "Test";
        }

        class TestDocument : BaseDocument
        {
            public List<string> Statuses = new List<string>() { "Active" };

            public Dictionary<string, object> Metadata = new Dictionary<string, object>();

            public override string Kind => "TestDocument";
        }
    }
}
