//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Handlers;
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
        public void ParametersReturnsQueryParameters()
        {
            var queryDefinition = new QueryDefinition("select * from s where s.Account = @account and s.Name = @name")
                .WithParameter("@account", "12345")
                .WithParameter("@name", "ABC");

            var parameters = queryDefinition.Parameters;
            Assert.IsTrue(parameters.TryGetValue("@account", out var account));
            Assert.AreEqual("12345", account);
            Assert.IsTrue(parameters.TryGetValue("@name", out var name));
            Assert.AreEqual("ABC", name);

            // Ensure the returned dictionary is not a copy, so modifications to the query
            // definition are reflected in the returned dictionary.
            queryDefinition.WithParameter("@foo", "bar");
            Assert.IsTrue(parameters.ContainsKey("@foo"));
        }

        [TestMethod]
        public void WithParametersCombinesWithExistingParameters()
        {
            var queryDefinition = new QueryDefinition("select * from s where s.Account = @account and s.Name = @name")
                .WithParameter("@account", "12345")
                .WithParameter("@name", "ABC");

            var parametersToAdd = new Dictionary<string, object>
            {
                ["@name"] = "XYZ",
                ["@foo"] = "bar"
            };

            queryDefinition.WithParameters(parametersToAdd);

            var parameters = queryDefinition.Parameters;
            Assert.IsTrue(parameters.TryGetValue("@account", out var account));
            Assert.AreEqual("12345", account);
            Assert.IsTrue(parameters.TryGetValue("@name", out var name));
            Assert.AreEqual("XYZ", name);
            Assert.IsTrue(parameters.TryGetValue("@foo", out var foo));
            Assert.AreEqual("bar", foo);
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
    }
}
