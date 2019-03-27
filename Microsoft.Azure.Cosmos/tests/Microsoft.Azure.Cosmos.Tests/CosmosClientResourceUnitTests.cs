//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.Client.Core.Tests;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Cosmos.Json;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

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

        [TestMethod]
        public void ValidateCosmosRequestOptionsClone()
        {
            CosmosQueryRequestOptions requestOptions = new CosmosQueryRequestOptions()
            {
                ConsistencyLevel = ConsistencyLevel.Strong,
                AccessCondition = new AccessCondition() { Type = AccessConditionType.IfMatch },
                EnableCrossPartitionQuery = true,
                EnableLowPrecisionOrderBy = true,
                EnableScanInQuery = true,
                ResponseContinuationTokenLimitInKb = 9001,
                PartitionKey = new PartitionKey("/test"),
                Properties = new Dictionary<string, object>() { { "test", "answer" } },
                RequestContinuation = "TestContinuationToken",
                MaxBufferedItemCount = 9002,
                MaxConcurrency = 9,
                MaxItemCount = 9003,
                SessionToken = "sessionTokenTest",
                CosmosSerializationOptions = new CosmosSerializationOptions(
                    ContentSerializationFormat.JsonText.ToString(),
                    (content) => JsonNavigator.Create(content),
                    () => JsonWriter.Create(JsonSerializationFormat.Binary))
            };

            // Verify that all the properties are cloned and the values match
            CosmosQueryRequestOptions clone = requestOptions.Clone();
            List<PropertyInfo> properties = requestOptions.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name).ToList();
            List<PropertyInfo> cloneProperties = clone.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).OrderBy(x => x.Name).ToList();
            Assert.AreEqual(properties.Count, cloneProperties.Count);

            for (int i = 0; i < properties.Count; i++)
            {
                Assert.AreEqual(properties[i].Name, cloneProperties[i].Name);
                Assert.AreEqual(properties[i].GetValue(requestOptions, null), cloneProperties[i].GetValue(clone, null));
            }

            // Verify that updating the original request option does not update the clone
            requestOptions.ConsistencyLevel = ConsistencyLevel.Eventual;
            requestOptions.AccessCondition = new AccessCondition() { Type = AccessConditionType.IfNoneMatch };
            requestOptions.EnableCrossPartitionQuery = false;
            requestOptions.EnableLowPrecisionOrderBy = false;
            requestOptions.EnableScanInQuery = false;
            requestOptions.ResponseContinuationTokenLimitInKb = 4200;
            requestOptions.PartitionKey = new PartitionKey("/updatedTest");
            requestOptions.Properties = new Dictionary<string, object>() { { "UpdatedTest", "UpdateAnswer" } };
            requestOptions.RequestContinuation = "UpdatedContinuationToken";
            requestOptions.MaxBufferedItemCount = 42001;
            requestOptions.MaxConcurrency = 42;
            requestOptions.MaxItemCount = 42003;
            requestOptions.SessionToken = "UpdatedSessionTokenTest";
            requestOptions.CosmosSerializationOptions = new CosmosSerializationOptions(
                ContentSerializationFormat.CosmosBinary.ToString(),
                (content) => JsonNavigator.Create(content),
                () => JsonWriter.Create(JsonSerializationFormat.Binary));

            for (int i = 0; i < properties.Count; i++)
            {
                Assert.AreEqual(properties[i].Name, cloneProperties[i].Name);
                Assert.AreNotEqual(properties[i].GetValue(requestOptions, null), cloneProperties[i].GetValue(clone, null), $"Property {properties[i].Name} not updated.");
            }
        }
    }
}
