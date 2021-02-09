//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Cosmos.Tracing.TraceData;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CosmosDiagnosticsUnitTests
    {
        [TestMethod]
        public void ValidateActivityScope()
        {
            Guid previousActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId;
            Guid testActivityId = Guid.NewGuid();
            System.Diagnostics.Trace.CorrelationManager.ActivityId = testActivityId;
            using (ActivityScope scope = new ActivityScope(Guid.NewGuid()))
            {
                Assert.AreNotEqual(Guid.Empty, System.Diagnostics.Trace.CorrelationManager.ActivityId, "Activity ID should not be the default");
                Assert.AreNotEqual(testActivityId, System.Diagnostics.Trace.CorrelationManager.ActivityId, "A new Activity ID should have set by the new ActivityScope");
                Assert.IsNull(ActivityScope.CreateIfDefaultActivityId());
            }

            Assert.AreEqual(testActivityId, System.Diagnostics.Trace.CorrelationManager.ActivityId, "Activity ID should be set back to previous version");
            System.Diagnostics.Trace.CorrelationManager.ActivityId = Guid.Empty;
            Assert.IsNotNull(ActivityScope.CreateIfDefaultActivityId());

            System.Diagnostics.Trace.CorrelationManager.ActivityId = previousActivityId;
        }

        [TestMethod]
        public void ValidateTransportHandlerLogging()
        {
            DocumentClientException dce = new DocumentClientException(
                "test",
                null,
                new StoreResponseNameValueCollection(),
                HttpStatusCode.Gone,
                SubStatusCodes.PartitionKeyRangeGone,
                new Uri("htts://localhost.com"));

            ITrace trace;
            RequestMessage requestMessage;
            using (trace = Cosmos.Tracing.Trace.GetRootTrace("testing"))
            {
                requestMessage = new RequestMessage(
                    HttpMethod.Get,
                    "/dbs/test/colls/abc/docs/123",
                    trace);
            }

            ResponseMessage response = dce.ToCosmosResponseMessage(requestMessage);

            Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode);
            Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, response.Headers.SubStatusCode);

            IEnumerable<PointOperationStatisticsTraceDatum> pointOperationStatistics = trace.Data.Values
                .Where(traceDatum => traceDatum is PointOperationStatisticsTraceDatum operationStatistics)
                .Select(x => (PointOperationStatisticsTraceDatum)x);

            if (pointOperationStatistics.Count() != 1)
            {
                Assert.Fail("PointOperationStatistics was not found in the diagnostics.");
            }

            PointOperationStatisticsTraceDatum operationStatistics = pointOperationStatistics.First();

            Assert.AreEqual(operationStatistics.StatusCode, HttpStatusCode.Gone);
            Assert.AreEqual(operationStatistics.SubStatusCode, SubStatusCodes.PartitionKeyRangeGone);
        }

        [TestMethod]
        public async Task ValidateActivityId()
        {
            using CosmosClient cosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            CosmosClientContext clientContext = ClientContextCore.Create(
              cosmosClient,
              new MockDocumentClient(),
              new CosmosClientOptions());

            Guid result = await clientContext.OperationHelperAsync<Guid>(
                nameof(ValidateActivityId),
                new RequestOptions(),
                (trace) => this.ValidateActivityIdHelper());

            Assert.AreEqual(Guid.Empty, System.Diagnostics.Trace.CorrelationManager.ActivityId, "ActivityScope was not disposed of");
        }

        [TestMethod]
        public async Task ValidateActivityIdWithSynchronizationContext()
        {
            Mock<SynchronizationContext> mockSynchronizationContext = new Mock<SynchronizationContext>()
            {
                CallBase = true
            };

            using CosmosClient cosmosClient = MockCosmosUtil.CreateMockCosmosClient();
            CosmosClientContext clientContext = ClientContextCore.Create(
                cosmosClient,
                new MockDocumentClient(),
                new CosmosClientOptions());

            try
            {
                SynchronizationContext.SetSynchronizationContext(mockSynchronizationContext.Object);

                Guid result = await clientContext.OperationHelperAsync<Guid>(
                    nameof(ValidateActivityIdWithSynchronizationContext),
                    new RequestOptions(),
                    (trace) => this.ValidateActivityIdHelper());

                Assert.AreEqual(Guid.Empty, System.Diagnostics.Trace.CorrelationManager.ActivityId, "ActivityScope was not disposed of");
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        private Task<Guid> ValidateActivityIdHelper()
        {
            Guid activityId = System.Diagnostics.Trace.CorrelationManager.ActivityId;
            Assert.AreNotEqual(Guid.Empty, activityId);
            return Task.FromResult(activityId);
        }
    }
}