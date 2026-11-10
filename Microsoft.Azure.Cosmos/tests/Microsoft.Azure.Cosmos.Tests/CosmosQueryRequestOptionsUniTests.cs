//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QueryRequestOptionsUniTests
    {
        [TestMethod]
        public void StatelessTest()
        {
            QueryRequestOptions requestOption = new QueryRequestOptions();
            using RequestMessage testMessage = new RequestMessage();
            requestOption.PopulateRequestOptions(testMessage);
            Assert.IsNull(testMessage.Headers.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow("[\"tenant\"]")]
        [DataRow("[\"tenant\",\"user\"]")]
        [DataRow("[null]")]
        [DataRow("[{}]")]
        [DataRow("[42.0,true]")]
        [DataRow("[\"escaped\\\"value\",\"back\\\\slash\"]")]
        public void PartialPartitionKeyHeaderTest(string json)
        {
            QueryRequestOptions options = CreateOptions(json);
            using RequestMessage request = CreateRequest();
            request.Headers.PartitionKeyRangeId = "0";
            request.Headers.StartEpk = "01";
            request.Headers.EndEpk = "02";

            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Gateway);
            options.PopulateRequestOptions(request);

            Assert.AreEqual(json, request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
            Assert.IsNull(request.Headers.PartitionKey);
            Assert.AreEqual("0", request.Headers.PartitionKeyRangeId);
            Assert.AreEqual("01", request.Headers.StartEpk);
            Assert.AreEqual("02", request.Headers.EndEpk);
            using RequestMessage clone = request.Clone(NoOpTrace.Singleton, cloneContent: null);
            Assert.AreEqual(json, clone.ToDocumentServiceRequest().Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [DataTestMethod]
        [DataRow("[\"tenant\"]", (int)PartitionKind.Hash, 1)]
        [DataRow("[\"tenant\"]", (int)PartitionKind.MultiHash, 1)]
        [DataRow("[\"tenant\",\"user\",\"session\"]", (int)PartitionKind.MultiHash, 3)]
        public void FullPartitionKeyKeepsExistingHeader(string json, int kind, int pathCount)
        {
            QueryRequestOptions options = CreateOptions(json);
            using RequestMessage request = CreateRequest();
            request.Headers.PartitionKey = json;
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition((PartitionKind)kind, pathCount), ConnectionMode.Direct);
            options.PopulateRequestOptions(request);
            Assert.AreEqual(json, request.Headers.PartitionKey);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [DataTestMethod]
        [DataRow("[]", (int)PartitionKind.MultiHash, 3)]
        [DataRow("[\"tenant\"]", (int)PartitionKind.Hash, 3)]
        [DataRow("[\"tenant\",\"user\",\"session\"]", (int)PartitionKind.MultiHash, 3)]
        [DataRow("[\"tenant\",\"user\",\"session\"]", (int)PartitionKind.MultiHash, 2)]
        public void NonPrefixKeyDoesNotAddPartialHeader(string json, int kind, int pathCount)
        {
            QueryRequestOptions options = CreateOptions(json);
            using RequestMessage request = CreateRequest();
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition((PartitionKind)kind, pathCount), ConnectionMode.Direct);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [DataTestMethod]
        [DataRow((int)OperationType.ReadFeed)]
        [DataRow((int)OperationType.QueryPlan)]
        [DataRow((int)OperationType.Read)]
        [DataRow((int)OperationType.Create)]
        public void NonQueryOperationsDoNotAddPartialHeader(int operation)
        {
            QueryRequestOptions options = CreateOptions("[\"tenant\"]");
            using RequestMessage request = CreateRequest((OperationType)operation);
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Direct);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
            Assert.IsNull(request.Headers.PartitionKey);
        }

        [TestMethod]
        public void NonDocumentQueryDoesNotAddPartialHeader()
        {
            QueryRequestOptions options = CreateOptions("[\"tenant\"]");
            using RequestMessage request = CreateRequest();
            request.ResourceType = ResourceType.Database;
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Direct);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [TestMethod]
        public void UnspecifiedAndNonePartitionKeysDoNotAddPartialHeader()
        {
            foreach (PartitionKey? partitionKey in new PartitionKey?[] { null, PartitionKey.None })
            {
                QueryRequestOptions options = new QueryRequestOptions
                {
                    PartitionKey = partitionKey,
                    EnablePartialPartitionKey = true
                };
                using RequestMessage request = CreateRequest();
                options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Direct);
                options.PopulateRequestOptions(request);
                Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
                Assert.IsNull(request.Headers.PartitionKey);
            }
        }

        [TestMethod]
        public void DisabledPartialPartitionKeyPreservesEpkRouting()
        {
            QueryRequestOptions options = CreateOptions("[\"tenant\"]");
            options.EnablePartialPartitionKey = false;
            using RequestMessage request = CreateRequest();
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Direct);
            options.PopulateRequestOptions(request);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
            Assert.IsNull(request.Headers.PartitionKey);
        }

        [TestMethod]
        public void DirectPartialPartitionKeyOptInFailsExplicitly()
        {
            QueryRequestOptions options = CreateOptions("[\"tenant\"]");
            using RequestMessage request = CreateRequest();
            Assert.ThrowsException<NotSupportedException>(() =>
                options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Direct));
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [TestMethod]
        public void ExistingLogicalPartitionKeyDoesNotGetPartialHeader()
        {
            QueryRequestOptions options = CreateOptions("[\"tenant\"]");
            using RequestMessage request = CreateRequest();
            request.Headers.PartitionKey = "[\"tenant\",\"user\",\"session\"]";
            options.PopulatePartialPartitionKeyHeader(request, CreateDefinition(), ConnectionMode.Gateway);
            Assert.AreEqual("[\"tenant\",\"user\",\"session\"]", request.Headers.PartitionKey);
            Assert.IsNull(request.Headers[QueryRequestOptions.PartialPartitionKeyHeader]);
        }

        [DataTestMethod]
        [DataRow(null, false)]
        [DataRow("false", false)]
        [DataRow("true", true)]
        [DoNotParallelize]
        public void PartialPartitionKeyRequiresEnvironmentOptIn(string value, bool expected)
        {
            string original = Environment.GetEnvironmentVariable(ConfigurationManager.PartialPartitionKeyEnabled);
            try
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartialPartitionKeyEnabled, value);
                QueryRequestOptions options = new QueryRequestOptions();
                Assert.AreEqual(expected, options.EnablePartialPartitionKey);
                Environment.SetEnvironmentVariable(ConfigurationManager.PartialPartitionKeyEnabled, (!expected).ToString());
                Assert.AreEqual(expected, options.EnablePartialPartitionKey);
                Assert.AreEqual(expected, ((QueryRequestOptions)options.ShallowCopy()).EnablePartialPartitionKey);
                Assert.AreEqual(!expected, new QueryRequestOptions().EnablePartialPartitionKey);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationManager.PartialPartitionKeyEnabled, original);
            }
        }

        private static QueryRequestOptions CreateOptions(string json)
        {
            Assert.IsTrue(PartitionKey.TryParseJsonString(json, out PartitionKey partitionKey));
            return new QueryRequestOptions { PartitionKey = partitionKey, EnablePartialPartitionKey = true };
        }

        private static PartitionKeyDefinition CreateDefinition(PartitionKind kind = PartitionKind.MultiHash, int pathCount = 3)
        {
            PartitionKeyDefinition definition = new PartitionKeyDefinition
            {
                Kind = kind,
                Version = Documents.PartitionKeyDefinitionVersion.V2,
                Paths = new Collection<string>()
            };
            for (int i = 0; i < pathCount; i++)
            {
                definition.Paths.Add("/key" + i);
            }

            return definition;
        }

        private static RequestMessage CreateRequest(OperationType operation = OperationType.Query) =>
            new RequestMessage(HttpMethod.Post, new Uri("dbs/test/colls/test/docs", UriKind.Relative))
            {
                ResourceType = ResourceType.Document,
                OperationType = operation
            };
    }
}
