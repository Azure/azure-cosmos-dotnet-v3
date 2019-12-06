//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Microsoft.Azure.Cosmos.CosmosClientSideRequestStatistics;

    [TestClass]
    public class PointOperationStatisticsTest
    {
        [TestMethod]
        public void ToStringTest()
        {

            CosmosClientSideRequestStatistics cosmosClientSideRequestStatistics = new CosmosClientSideRequestStatistics();
            //Setting null supplementalResponseStatisticsList
            cosmosClientSideRequestStatistics.supplementalResponseStatisticsList = null;
            PointOperationStatistics pointOperationStatistics = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: System.Net.HttpStatusCode.OK,
                subStatusCode: Documents.SubStatusCodes.Unknown,
                requestCharge: 42,
                errorMessage: null,
                method: HttpMethod.Get,
                requestUri: new System.Uri("https://localhost:8081"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics);

            pointOperationStatistics.ToString();
            Assert.IsNull(pointOperationStatistics.ClientSideRequestStatistics.supplementalResponseStatisticsList);

            //Adding 5 objects supplementalResponseStatisticsList
            cosmosClientSideRequestStatistics.supplementalResponseStatisticsList = new List<StoreResponseStatistics>
            {
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics()
            };

            pointOperationStatistics = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: System.Net.HttpStatusCode.OK,
                subStatusCode: Documents.SubStatusCodes.Unknown,
                requestCharge: 42,
                errorMessage: null,
                method: HttpMethod.Get,
                requestUri: new System.Uri("https://localhost:8081"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics: cosmosClientSideRequestStatistics);
            pointOperationStatistics.ToString();
            Assert.AreEqual(5, pointOperationStatistics.ClientSideRequestStatistics.supplementalResponseStatisticsList.Count);

            //Adding 5 more objects supplementalResponseStatisticsList, making total 10
            cosmosClientSideRequestStatistics.supplementalResponseStatisticsList.AddRange(new List<StoreResponseStatistics>()
            {
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics(),
                new StoreResponseStatistics()
            });

            pointOperationStatistics = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: System.Net.HttpStatusCode.OK,
                subStatusCode: Documents.SubStatusCodes.Unknown,
                requestCharge: 42,
                errorMessage: null,
                method: HttpMethod.Get,
                requestUri: new System.Uri("https://localhost:8081"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics:  cosmosClientSideRequestStatistics);
            pointOperationStatistics.ToString();
            Assert.AreEqual(10, pointOperationStatistics.ClientSideRequestStatistics.supplementalResponseStatisticsList.Count);

            //Adding 2 more objects supplementalResponseStatisticsList, making total 12
            cosmosClientSideRequestStatistics.supplementalResponseStatisticsList.AddRange(new List<StoreResponseStatistics>()
            {
                new StoreResponseStatistics(),
                new StoreResponseStatistics()
            });

            pointOperationStatistics = new PointOperationStatistics(
                activityId: Guid.NewGuid().ToString(),
                statusCode: System.Net.HttpStatusCode.OK,
                subStatusCode: Documents.SubStatusCodes.Unknown,
                requestCharge: 42,
                errorMessage: null,
                method: HttpMethod.Get,
                requestUri: new System.Uri("https://localhost:8081"),
                requestSessionToken: null,
                responseSessionToken: null,
                clientSideRequestStatistics:  cosmosClientSideRequestStatistics);
            pointOperationStatistics.ToString();
            Assert.AreEqual(10, pointOperationStatistics.ClientSideRequestStatistics.supplementalResponseStatisticsList.Count);
        }
    }
}
