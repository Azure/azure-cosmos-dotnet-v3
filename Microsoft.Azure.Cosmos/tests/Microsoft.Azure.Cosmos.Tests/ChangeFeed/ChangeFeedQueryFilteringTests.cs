//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.IO;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedQueryFilteringTests
    {
        [DataTestMethod]
        [DataRow("changeFeedStartFrom")]
        [DataRow("changeFeedMode")]
        [DataRow("queryText")]
        [DataRow("changeFeedRequestOptions")]
        public void QueryApisValidateRequiredArguments(string parameterName)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("db", "items");
            ChangeFeedStartFrom start = parameterName == "changeFeedStartFrom" ? null : ChangeFeedStartFrom.Beginning();
            ChangeFeedMode mode = parameterName == "changeFeedMode" ? null : ChangeFeedMode.LatestVersion;
            string query = parameterName == "queryText" ? null : "SELECT * FROM c";
            ChangeFeedRequestOptions options = parameterName == "changeFeedRequestOptions" ? null : new ChangeFeedRequestOptions();
            Assert.AreEqual(parameterName, Assert.ThrowsException<ArgumentNullException>(
                () => container.GetChangeFeedStreamIteratorWithQuery(start, mode, query, options)).ParamName);
            Assert.AreEqual(parameterName, Assert.ThrowsException<ArgumentNullException>(
                () => container.GetChangeFeedIteratorWithQuery<object>(start, mode, query, options)).ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow(" \t\r\n")]
        public void QueryApisRejectBlankText(string query)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("db", "items");
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions();
            Assert.AreEqual("queryText", Assert.ThrowsException<ArgumentException>(
                () => container.GetChangeFeedStreamIteratorWithQuery(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, query, options)).ParamName);
            Assert.AreEqual("queryText", Assert.ThrowsException<ArgumentException>(
                () => container.GetChangeFeedIteratorWithQuery<object>(ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, query, options)).ParamName);
        }

        [TestMethod]
        public void QueryApisCreateExpectedIterators()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Container container = client.GetContainer("db", "items");
            ChangeFeedRequestOptions options = new ChangeFeedRequestOptions();
            using FeedIterator stream = container.GetChangeFeedStreamIteratorWithQuery(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, "SELECT * FROM c", options);
            using FeedIterator<object> typed = container.GetChangeFeedIteratorWithQuery<object>(
                ChangeFeedStartFrom.Beginning(), ChangeFeedMode.LatestVersion, "SELECT * FROM c", options);
            Assert.IsTrue(stream.HasMoreResults);
            Assert.IsTrue(typed.HasMoreResults);
            Assert.IsInstanceOfType(typed, typeof(FeedIteratorInlineCore<object>));
        }

        [DataTestMethod]
        [DataRow(nameof(Container.GetChangeFeedStreamIteratorWithQuery))]
        [DataRow(nameof(Container.GetChangeFeedIteratorWithQuery))]
        public void QueryApisHaveNoOptionalParameters(string methodName)
        {
            MethodInfo method = typeof(Container).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            ParameterInfo[] parameters = method.GetParameters();
            Assert.AreEqual(4, parameters.Length);
            foreach (ParameterInfo parameter in parameters)
            {
                Assert.IsFalse(parameter.IsOptional, parameter.Name);
                Assert.IsFalse(parameter.HasDefaultValue, parameter.Name);
            }
        }

        [TestMethod]
        public void NullQueryIsRejected()
        {
            Assert.AreEqual("queryText", Assert.ThrowsException<ArgumentNullException>(
                () => ChangeFeedHelper.CreateQuerySpec(null)).ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" \t\r\n")]
        public void BlankQueriesAreRejected(string query)
        {
            Assert.AreEqual("queryText", Assert.ThrowsException<ArgumentException>(
                () => ChangeFeedHelper.CreateQuerySpec(query)).ParamName);
        }

        [TestMethod]
        public void QueryPayloadUsesInternalSerializerAndPreservesText()
        {
            const string query = " SELECT * FROM c WHERE c.state = \"WA\" ";
            Mock<CosmosSerializer> customerSerializer = new Mock<CosmosSerializer>(MockBehavior.Strict);
            CosmosSerializerCore serializer = new CosmosSerializerCore(customerSerializer.Object);
            using Stream payload = serializer.ToStream(ChangeFeedHelper.CreateQuerySpec(query));
            using StreamReader reader = new StreamReader(payload);
            JObject body = JObject.Parse(reader.ReadToEnd());
            Assert.AreEqual(query, (string)body["query"]);
            Assert.IsFalse((bool)body["enableQueryOnPreviousImage"]);
            Assert.AreEqual(2, body.Count);
            customerSerializer.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void MissingQueryHasNoFingerprint()
        {
            Assert.IsNull(ChangeFeedHelper.GetQueryFilterHash(null));
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c WHERE c.state = \"WA\"", "eWdVXz97p6d0Din52ndNUubhHkoWGGoYEaKKE5WbZvM=")]
        [DataRow("SELECT * FROM c WHERE c.name = \"\u00e9\"", "qVrjx4ZOhewtcaScQTjBB0Pao022eix/4vOR8nLVGAM=")]
        public void QueryFingerprintUsesFullUtf8Sha256(string query, string expectedHash)
        {
            string hash = ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(query));
            Assert.AreEqual(expectedHash, hash);
            Assert.AreEqual(32, Convert.FromBase64String(hash).Length);
            Assert.AreEqual(hash, ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(query)));
        }

        [DataTestMethod]
        [DataRow("SELECT * FROM c WHERE c.state = \"OR\"")]
        [DataRow(" SELECT * FROM c WHERE c.state = \"WA\"")]
        [DataRow("SELECT * FROM c WHERE c.state = \"WA\" ")]
        [DataRow("select * FROM c WHERE c.state = \"WA\"")]
        [DataRow("SELECT * FROM c\nWHERE c.state = \"WA\"")]
        public void QueryFingerprintPreservesTextDifferences(string query)
        {
            const string originalQuery = "SELECT * FROM c WHERE c.state = \"WA\"";
            Assert.AreNotEqual(
                ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(originalQuery)),
                ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(query)));
        }
    }
}
