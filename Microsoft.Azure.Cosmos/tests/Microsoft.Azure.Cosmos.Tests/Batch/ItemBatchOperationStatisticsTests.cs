//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using Microsoft.Azure.Cosmos.Diagnostics;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ItemBatchOperationStatisticsTests
    {
        [TestMethod]
        public void ToString_WhenEmptyReturnEmptyString()
        {
            ItemBatchOperationStatistics itemBatchOperationStatistics = new ItemBatchOperationStatistics();
            Assert.IsTrue(string.IsNullOrEmpty(itemBatchOperationStatistics.ToString()));
        }

        [TestMethod]
        public void ToString_ReturnItemsToString()
        {
            ItemBatchOperationStatistics itemBatchOperationStatistics = new ItemBatchOperationStatistics();

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics1 = new CosmosClientSideRequestStatistics();
            cosmosClientSideRequestStatistics1.ContactedReplicas.Add(new Uri("https://one.com"));
            PointOperationStatistics pointOperation1 = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: new Uri("http://localhost"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics1);

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics2 = new CosmosClientSideRequestStatistics();
            cosmosClientSideRequestStatistics2.ContactedReplicas.Add(new Uri("https://two.com"));
            PointOperationStatistics pointOperation2 = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: new Uri("http://localhost"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics2);

            itemBatchOperationStatistics.AppendDiagnostics(pointOperation1);
            itemBatchOperationStatistics.AppendDiagnostics(pointOperation2);

            string toString = itemBatchOperationStatistics.ToString();

            Assert.IsTrue(toString.Contains(pointOperation1.ToString()));
            Assert.IsTrue(toString.Contains(pointOperation2.ToString()));
        }

        [TestMethod]
        public void Complete_AddsCompleteTime()
        {
            ItemBatchOperationStatistics itemBatchOperationStatistics = new ItemBatchOperationStatistics();

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics1 = new CosmosClientSideRequestStatistics();
            cosmosClientSideRequestStatistics1.ContactedReplicas.Add(new Uri("https://one.com"));
            PointOperationStatistics pointOperation1 = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: new Uri("http://localhost"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics1);

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics2 = new CosmosClientSideRequestStatistics();
            cosmosClientSideRequestStatistics2.ContactedReplicas.Add(new Uri("https://two.com"));
            PointOperationStatistics pointOperation2 = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: HttpStatusCode.OK,
                subStatusCode: SubStatusCodes.Unknown,
                requestCharge: 0,
                errorMessage: string.Empty,
                method: HttpMethod.Get,
                requestUri: new Uri("http://localhost"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics2);

            itemBatchOperationStatistics.AppendDiagnostics(pointOperation1);
            itemBatchOperationStatistics.AppendDiagnostics(pointOperation2);
            itemBatchOperationStatistics.Complete();

            Assert.IsTrue(itemBatchOperationStatistics.ToString().Contains("Completed at"));
        }
    }
}
