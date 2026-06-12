//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class MetadataHedgingRegionalFailureTests
    {
        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_HttpRequestException_ReturnsTrue()
        {
            Assert.IsTrue(MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: null,
                subStatus: SubStatusCodes.Unknown,
                exception: new HttpRequestException("boom"),
                callerToken: CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_NonUserOperationCanceled_ReturnsTrue()
        {
            Assert.IsTrue(MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: null,
                subStatus: SubStatusCodes.Unknown,
                exception: new OperationCanceledException(),
                callerToken: CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_UserOperationCanceled_ReturnsFalse()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();
            Assert.IsFalse(MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: null,
                subStatus: SubStatusCodes.Unknown,
                exception: new OperationCanceledException(),
                callerToken: cts.Token));
        }

        [TestMethod]
        [Owner("dkunda")]
        [DataRow((int)HttpStatusCode.ServiceUnavailable, true)]
        [DataRow((int)HttpStatusCode.InternalServerError, true)]
        [DataRow((int)HttpStatusCode.NotFound, false)]
        [DataRow((int)HttpStatusCode.Unauthorized, false)]
        [DataRow((int)HttpStatusCode.OK, false)]
        public void IsRegionalFailure_StatusCodeOnly(int statusCode, bool expected)
        {
            Assert.AreEqual(expected, MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: (HttpStatusCode)statusCode,
                subStatus: SubStatusCodes.Unknown,
                exception: null,
                callerToken: CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_GoneRequiresLeaseNotFound()
        {
            Assert.IsTrue(MetadataHedgingStrategy.IsRegionalFailure(
                HttpStatusCode.Gone, SubStatusCodes.LeaseNotFound, null, CancellationToken.None));
            Assert.IsFalse(MetadataHedgingStrategy.IsRegionalFailure(
                HttpStatusCode.Gone, SubStatusCodes.PartitionKeyRangeGone, null, CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_ForbiddenRequiresDatabaseAccountNotFound()
        {
            Assert.IsTrue(MetadataHedgingStrategy.IsRegionalFailure(
                HttpStatusCode.Forbidden, SubStatusCodes.DatabaseAccountNotFound, null, CancellationToken.None));
            Assert.IsFalse(MetadataHedgingStrategy.IsRegionalFailure(
                HttpStatusCode.Forbidden, SubStatusCodes.Unknown, null, CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void IsRegionalFailure_NoStatusNoException_ReturnsFalse()
        {
            Assert.IsFalse(MetadataHedgingStrategy.IsRegionalFailure(
                statusCode: null,
                subStatus: SubStatusCodes.Unknown,
                exception: null,
                callerToken: CancellationToken.None));
        }

        [TestMethod]
        [Owner("dkunda")]
        public void HttpTimeoutPolicyControlPlaneRetriableHotPath_FirstAttemptTimeout_Is1Second()
        {
            Assert.AreEqual(
                TimeSpan.FromSeconds(1),
                HttpTimeoutPolicyControlPlaneRetriableHotPath.Instance.FirstAttemptTimeout);
        }
    }
}
