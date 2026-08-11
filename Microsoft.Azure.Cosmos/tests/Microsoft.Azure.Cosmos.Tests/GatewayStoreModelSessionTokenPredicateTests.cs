//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System.Net;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Truth tables pinning the session-token policy predicates extracted from
    /// <see cref="GatewayStoreModel"/>. These predicates are shared with the distributed-transaction
    /// path, so they must stay behaviorally identical to the inline expressions they replaced.
    /// </summary>
    [TestClass]
    public class GatewayStoreModelSessionTokenPredicateTests
    {
        [TestMethod]
        [Owner("mpalaparthi")]
        [DataRow(HttpStatusCode.PreconditionFailed, (int)SubStatusCodes.Unknown, true, DisplayName = "412 captures")]
        [DataRow(HttpStatusCode.Conflict, (int)SubStatusCodes.Unknown, true, DisplayName = "409 captures")]
        [DataRow(HttpStatusCode.NotFound, (int)SubStatusCodes.Unknown, true, DisplayName = "404 with no substatus captures")]
        [DataRow(HttpStatusCode.NotFound, (int)SubStatusCodes.OwnerResourceNotFound, true, DisplayName = "404 with a non-1002 substatus captures")]
        [DataRow(HttpStatusCode.NotFound, (int)SubStatusCodes.ReadSessionNotAvailable, false, DisplayName = "404/1002 does not capture")]
        [DataRow(HttpStatusCode.OK, (int)SubStatusCodes.Unknown, false, DisplayName = "200 is not an error status")]
        [DataRow(HttpStatusCode.Created, (int)SubStatusCodes.Unknown, false, DisplayName = "201 is not an error status")]
        [DataRow(HttpStatusCode.FailedDependency, (int)SubStatusCodes.Unknown, false, DisplayName = "424 does not capture")]
        [DataRow((HttpStatusCode)429, (int)SubStatusCodes.Unknown, false, DisplayName = "429 does not capture")]
        [DataRow(HttpStatusCode.Gone, (int)SubStatusCodes.PartitionKeyRangeGone, false, DisplayName = "410 does not capture")]
        [DataRow(HttpStatusCode.ServiceUnavailable, (int)SubStatusCodes.Unknown, false, DisplayName = "503 does not capture")]
        [DataRow(HttpStatusCode.RequestTimeout, (int)SubStatusCodes.Unknown, false, DisplayName = "408 does not capture")]
        [DataRow(HttpStatusCode.InternalServerError, (int)SubStatusCodes.Unknown, false, DisplayName = "500 does not capture")]
        public void IsSessionTokenCapturableErrorStatus_MatchesPointOperationPolicy(
            HttpStatusCode statusCode,
            int subStatusCode,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                GatewayStoreModel.IsSessionTokenCapturableErrorStatus(statusCode, (SubStatusCodes)subStatusCode),
                $"Capture policy changed for {statusCode}/{subStatusCode}.");
        }

        [TestMethod]
        [Owner("mpalaparthi")]
        public void IsSessionTokenCapturableErrorStatus_NullStatusDoesNotCapture()
        {
            Assert.IsFalse(
                GatewayStoreModel.IsSessionTokenCapturableErrorStatus(null, SubStatusCodes.Unknown),
                "A missing status code carries no trustworthy progress and must not capture.");
        }

        [TestMethod]
        [Owner("mpalaparthi")]
        // The distributed transaction result exposes a non-nullable status, so an absent per-operation
        // status reaches this predicate as the default zero value rather than as null. Both must be
        // covered or the zero case is only ever exercised by accident.
        public void IsSessionTokenCapturableErrorStatus_DefaultStatusDoesNotCapture()
        {
            Assert.IsFalse(
                GatewayStoreModel.IsSessionTokenCapturableErrorStatus((HttpStatusCode)0, SubStatusCodes.Unknown),
                "An unset status code carries no trustworthy progress and must not capture.");
        }

        [TestMethod]
        [Owner("mpalaparthi")]
        // Account is Session, no per-request override: the read/write and multi-master axes decide.
        [DataRow(ConsistencyLevel.Session, null, true, false, true, DisplayName = "Session account, read, single-master -> applies")]
        [DataRow(ConsistencyLevel.Session, null, true, true, true, DisplayName = "Session account, read, multi-master -> applies")]
        [DataRow(ConsistencyLevel.Session, null, false, true, true, DisplayName = "Session account, write, multi-master -> applies")]
        [DataRow(ConsistencyLevel.Session, null, false, false, false, DisplayName = "Session account, write, single-master -> gated out")]
        // Non-session accounts never apply a token without a per-request override.
        [DataRow(ConsistencyLevel.Eventual, null, true, false, false, DisplayName = "Eventual account, read -> does not apply")]
        [DataRow(ConsistencyLevel.Strong, null, true, false, false, DisplayName = "Strong account, read -> does not apply")]
        [DataRow(ConsistencyLevel.Eventual, null, false, true, false, DisplayName = "Eventual account, multi-master write -> does not apply")]
        // A read may opt in to session consistency on a non-session account.
        [DataRow(ConsistencyLevel.Eventual, "Session", true, false, true, DisplayName = "Eventual account, read overriding to Session -> applies")]
        [DataRow(ConsistencyLevel.Eventual, "session", true, false, true, DisplayName = "Override match is case-insensitive")]
        // A read may also opt out of session consistency on a Session account.
        [DataRow(ConsistencyLevel.Session, "Eventual", true, false, false, DisplayName = "Session account, read overriding to Eventual -> does not apply")]
        // Writes cannot override consistency, so the override is ignored and the account value stands.
        [DataRow(ConsistencyLevel.Session, "Eventual", false, true, true, DisplayName = "Write ignores its consistency override on a multi-master Session account")]
        [DataRow(ConsistencyLevel.Session, "Eventual", false, false, false, DisplayName = "Write ignores its override but is still gated on single-master")]
        [DataRow(ConsistencyLevel.Eventual, "Session", false, true, false, DisplayName = "Write cannot opt in to session consistency")]
        // An unrecognized override is treated as set-but-not-session rather than falling back to the account value.
        [DataRow(ConsistencyLevel.Session, "NotAConsistencyLevel", true, false, false, DisplayName = "Unparseable override on a read does not fall back to the account value")]
        // An empty override is indistinguishable from no override.
        [DataRow(ConsistencyLevel.Session, "", true, false, true, DisplayName = "Empty override is treated as absent")]
        public void IsSessionTokenApplicable_MatchesPointOperationGate(
            ConsistencyLevel defaultConsistencyLevel,
            string requestConsistencyLevel,
            bool isReadOrBatchRequest,
            bool isMultiMasterEnabled,
            bool expected)
        {
            Assert.AreEqual(
                expected,
                GatewayStoreModel.IsSessionTokenApplicable(
                    defaultConsistencyLevel,
                    requestConsistencyLevel,
                    isReadOrBatchRequest,
                    isMultiMasterEnabled),
                $"Gate changed for account={defaultConsistencyLevel}, override={requestConsistencyLevel ?? "<null>"}, " +
                $"isReadOrBatch={isReadOrBatchRequest}, isMultiMaster={isMultiMasterEnabled}.");
        }
    }
}
