//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
            SqlParameterCollection sqlParameters = new SqlParameterCollection
            {
                new SqlParameter("@account", "12345"),
                new SqlParameter("@name", "ABC")
            };
            sqlQuerySpec = new SqlQuerySpec(query, sqlParameters);
            sqlQueryDefinition = QueryDefinition.CreateFromQuerySpec(sqlQuerySpec);
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
        public void GetQueryParametersReturnsListOfTuples()
        {
            string query = "select * from s where s.Account = @account and s.Balance > @balance";
            QueryDefinition queryDefinition = new QueryDefinition(query)
                .WithParameter("@account", "12345")
                .WithParameter("@balance", 42);

            IReadOnlyList<(string Name, object Value)> parameters = queryDefinition.GetQueryParameters();

            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("@account", parameters[0].Name);
            Assert.AreEqual("12345", parameters[0].Value);
            Assert.AreEqual("@balance", parameters[1].Name);
            Assert.AreEqual(42, parameters[1].Value);
        }

        [TestMethod]
        public void GetQueryParametersAlwaysReturnsTheSameInstance()
        {
            string query = "select * from s where s.Account = @account and s.Balance > @balance";
            QueryDefinition queryDefinition = new QueryDefinition(query)
                .WithParameter("@account", "12345")
                .WithParameter("@balance", 42);

            IReadOnlyList<(string Name, object Value)> parameters1 = queryDefinition.GetQueryParameters();
            IReadOnlyList<(string Name, object Value)> parameters2 = queryDefinition.GetQueryParameters();

            Assert.AreSame(parameters1, parameters2);
        }

        [TestMethod]
        public void GetQueryParametersReflectsParametersAddedLater()
        {
            string query = "select * from s where s.Account = @account and s.Balance > @balance";
            QueryDefinition queryDefinition = new QueryDefinition(query)
                .WithParameter("@account", "12345");

            IReadOnlyList<(string Name, object Value)> parameters = queryDefinition.GetQueryParameters();

            queryDefinition.WithParameter("@balance", 42);

            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("@account", parameters[0].Name);
            Assert.AreEqual("12345", parameters[0].Value);
            Assert.AreEqual("@balance", parameters[1].Name);
            Assert.AreEqual(42, parameters[1].Value);
        }

        [TestMethod]
        public void GetQueryParametersReflectsParametersChangedLater()
        {
            string query = "select * from s where s.Account = @account and s.Balance > @balance";
            QueryDefinition queryDefinition = new QueryDefinition(query)
                .WithParameter("@account", "12345")
                .WithParameter("@balance", 42);

            IReadOnlyList<(string Name, object Value)> parameters = queryDefinition.GetQueryParameters();

            queryDefinition.WithParameter("@balance", 123);

            Assert.AreEqual(2, parameters.Count);
            Assert.AreEqual("@account", parameters[0].Name);
            Assert.AreEqual("12345", parameters[0].Value);
            Assert.AreEqual("@balance", parameters[1].Name);
            Assert.AreEqual(123, parameters[1].Value);
        }

        [TestMethod]
        public void GetQueryParametersGetEnumeratorEnumeratesParameters()
        {
            string query = "select * from s where s.Account = @account and s.Balance > @balance";
            QueryDefinition queryDefinition = new QueryDefinition(query)
                .WithParameter("@account", "12345")
                .WithParameter("@balance", 42);

            IReadOnlyList<(string Name, object Value)> parameters = queryDefinition.GetQueryParameters();
            IEnumerator<(string Name, object Value)> enumerator = parameters.GetEnumerator();

            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("@account", enumerator.Current.Name);
            Assert.AreEqual("12345", enumerator.Current.Value);
            Assert.IsTrue(enumerator.MoveNext());
            Assert.AreEqual("@balance", enumerator.Current.Name);
            Assert.AreEqual(42, enumerator.Current.Value);
            Assert.IsFalse(enumerator.MoveNext());
        }
    }
}