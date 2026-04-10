//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryDefinitionComparerUnitTests
    {

        [TestMethod]
        public void ValidateHashWithSameParameter()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", "12345");
            sqlQueryDefinition.WithParameter("@name", "ABC");

            int hashCode = QueryDefinitionEqualityComparer.Instance.GetHashCode(sqlQueryDefinition);

            // Reverse the order
            sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@name", "ABC");
            sqlQueryDefinition.WithParameter("@account", "12345");

            Assert.AreEqual(hashCode, QueryDefinitionEqualityComparer.Instance.GetHashCode(sqlQueryDefinition));
        }

        [TestMethod]
        public void ValidateHashWithNullValue()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", null);

            int hashCode = QueryDefinitionEqualityComparer.Instance.GetHashCode(sqlQueryDefinition);

            // Reverse the order
            sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", null);

            Assert.AreEqual(hashCode, QueryDefinitionEqualityComparer.Instance.GetHashCode(sqlQueryDefinition));
        }

        [TestMethod]
        public void ValidateEqualsWithParameters()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", "12345");
            sqlQueryDefinition.WithParameter("@name", "ABC");

            // Reverse the order
            QueryDefinition sqlQueryDefinition2 = new QueryDefinition("select * from s where s.Account = 1234");

            Assert.IsFalse(QueryDefinitionEqualityComparer.Instance.Equals(sqlQueryDefinition, sqlQueryDefinition2));
            string param = new string(new[] { 'A', 'B', 'C' });
            sqlQueryDefinition2.WithParameter("@name", param);
            sqlQueryDefinition2.WithParameter("@account", "12345");

            Assert.IsTrue(QueryDefinitionEqualityComparer.Instance.Equals(sqlQueryDefinition, sqlQueryDefinition2));
        }
    }
}