//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryDefinitionUnitTests
    {
        [TestMethod]
        public void ValidateCreateQueryDefinition()
        {
            string query = "select * from s where s.Account = @account";
            string paramName = "@account";
            string paramValue = "12345";
            QueryDefinition sqlQueryDefinition = new QueryDefinition(query)
                .WithParameter(paramName, paramValue);

            SqlQuerySpec sqlQuerySpec = sqlQueryDefinition.ToSqlQuerySpec();
            Assert.AreEqual(query, sqlQuerySpec.QueryText);
            Assert.AreEqual(1, sqlQuerySpec.Parameters.Count);
            SqlParameter sqlParameter = sqlQuerySpec.Parameters.First();
            Assert.AreEqual(paramName, sqlParameter.Name);
            Assert.AreEqual(paramValue, sqlParameter.Value);

            string newParamValue = "9001";
            sqlQueryDefinition.WithParameter(paramName, newParamValue);
            sqlQuerySpec = sqlQueryDefinition.ToSqlQuerySpec();
            Assert.AreEqual(query, sqlQuerySpec.QueryText);
            Assert.AreEqual(1, sqlQuerySpec.Parameters.Count);
            sqlParameter = sqlQuerySpec.Parameters.First();
            Assert.AreEqual(paramName, sqlParameter.Name);
            Assert.AreEqual(newParamValue, sqlParameter.Value);

            query = "select * from s where s.Account = @account and s.Name = @name";
            SqlParameterCollection sqlParameters = new SqlParameterCollection();
            sqlParameters.Add(new SqlParameter("@account", "12345"));
            sqlParameters.Add(new SqlParameter("@name", "ABC"));
            sqlQuerySpec = new SqlQuerySpec(query, sqlParameters);
            sqlQueryDefinition = new QueryDefinition(sqlQuerySpec);
            Assert.AreEqual(sqlQueryDefinition.QueryText, sqlQuerySpec.QueryText);
            Assert.AreEqual(sqlQueryDefinition.ToSqlQuerySpec().QueryText, sqlQueryDefinition.QueryText);
            Assert.AreEqual(sqlQueryDefinition.ToSqlQuerySpec().Parameters.Count(), sqlQuerySpec.Parameters.Count());
            Assert.AreEqual(sqlQueryDefinition.ToSqlQuerySpec().Parameters.First().Name, sqlQuerySpec.Parameters.First().Name);
            Assert.AreEqual(sqlQueryDefinition.ToSqlQuerySpec().Parameters.First().Value, sqlQuerySpec.Parameters.First().Value);

        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullQueryText()
        {
            new QueryDefinition(query: null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ThrowOnNullConnectionString()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter(null, null);
        }


        [TestMethod]
        public void ValidateHashWithSameParameter()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", "12345");
            sqlQueryDefinition.WithParameter("@name", "ABC");

            int hashCode = sqlQueryDefinition.GetHashCode();

            // Reverse the order
            sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@name", "ABC");
            sqlQueryDefinition.WithParameter("@account", "12345");

            Assert.AreEqual(hashCode, sqlQueryDefinition.GetHashCode());
        }

        [TestMethod]
        public void ValidateHashWithNullValue()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", null);

            int hashCode = sqlQueryDefinition.GetHashCode();

            // Reverse the order
            sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", null);

            Assert.AreEqual(hashCode, sqlQueryDefinition.GetHashCode());
        }

        [TestMethod]
        public void ValidateEqualsWithParameters()
        {
            QueryDefinition sqlQueryDefinition = new QueryDefinition("select * from s where s.Account = 1234");
            sqlQueryDefinition.WithParameter("@account", "12345");
            sqlQueryDefinition.WithParameter("@name", "ABC");

            // Reverse the order
            QueryDefinition sqlQueryDefinition2 = new QueryDefinition("select * from s where s.Account = 1234");

            Assert.IsFalse(sqlQueryDefinition.Equals(sqlQueryDefinition2));

            sqlQueryDefinition2.WithParameter("@name", "ABC");
            sqlQueryDefinition2.WithParameter("@account", "12345");

            Assert.IsTrue(sqlQueryDefinition.Equals(sqlQueryDefinition2));
        }
    }
}
