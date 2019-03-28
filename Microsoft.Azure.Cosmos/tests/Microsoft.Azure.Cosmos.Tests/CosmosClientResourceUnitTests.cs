//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosClientResourceUnitTests
    {
        [TestMethod]
        public void ValidateUriGenerationForResources()
        {
            string databaseId = "db1234";
            string crId = "cr42";
            string spId = "sp9001";
            string trId = "tr9002";
            string udfId = "udf9003";

            CosmosClient mockClient = MockDocumentClient.CreateMockCosmosClient();
            CosmosDatabase db = new CosmosDatabase(mockClient, databaseId);
            Assert.AreEqual(db.Link, "/dbs/" + databaseId);

            CosmosContainer container = new CosmosContainer(db, crId);
            Assert.AreEqual(container.Link, "/dbs/" + databaseId + "/colls/" + crId);

            CosmosStoredProcedure sp = new CosmosStoredProcedure(container, spId);
            Assert.AreEqual(sp.Link, "/dbs/" + databaseId + "/colls/" + crId + "/sprocs/" + spId);

            CosmosTrigger tr = new CosmosTrigger(container, trId);
            Assert.AreEqual(tr.Link, "/dbs/" + databaseId + "/colls/" + crId + "/triggers/" + trId);

            CosmosUserDefinedFunction udf = new CosmosUserDefinedFunction(container, udfId);
            Assert.AreEqual(udf.Link, "/dbs/" + databaseId + "/colls/" + crId + "/udfs/" + udfId);
        }

        [TestMethod]
        public void ValidateItemRequestOptions()
        {
            CosmosItemRequestOptions options = new CosmosItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger"
                }
            };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

        [TestMethod]
        public void ValidateItemRequestOptionsMultipleTriggers()
        {
            CosmosItemRequestOptions options = new CosmosItemRequestOptions
            {
                PreTriggers = new List<string>()
                {
                    "preTrigger",
                    "preTrigger2",
                    "preTrigger3",
                    "preTrigger4"
                },

                PostTriggers = new List<string>()
                {
                    "postTrigger",
                    "postTrigger2",
                    "postTrigger3",
                    "postTrigger4",
                    "postTrigger5"
                }
            };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

        [TestMethod]
        public void ValidateSetItemRequestOptions()
        {
            CosmosItemRequestOptions options = new CosmosItemRequestOptions();
            options.PreTriggers = new List<string>() { "preTrigger" };
            options.PostTriggers = new List<string>() { "postTrigger" };

            CosmosRequestMessage httpRequest = new CosmosRequestMessage(
                HttpMethod.Post,
                new Uri("/dbs/testdb/colls/testcontainer/docs/testId", UriKind.Relative));

            options.FillRequestOptions(httpRequest);

            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PreTriggerInclude, out string preTriggerHeader));
            Assert.IsTrue(httpRequest.Headers.TryGetValue(HttpConstants.HttpHeaders.PostTriggerInclude, out string postTriggerHeader));
        }

    }
}
