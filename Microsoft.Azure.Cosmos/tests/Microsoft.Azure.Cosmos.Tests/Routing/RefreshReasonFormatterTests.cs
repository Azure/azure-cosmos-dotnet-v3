//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="RefreshReason"/> and <see cref="RefreshReasonExtensions"/>.
    /// These invariants protect the design-time-bounded contract: every
    /// enum member has a wire value, every wire value is safe for HTTP
    /// headers / log pipelines, and every <see cref="TransportErrorCode"/>
    /// maps to a reason.
    /// </summary>
    [TestClass]
    public class RefreshReasonFormatterTests
    {
        // Allowed charset on the wire: lowercase letters, digits, underscore,
        // dot; plus uppercase (for the camel/Pascal-cased Group-D values like
        // "InsufficientReplicas.Quorum"). Two dot-separated segments at most.
        private static readonly Regex WireValueRegex = new Regex(
            @"^[A-Za-z0-9_]+(\.[A-Za-z0-9_]+)?$",
            RegexOptions.Compiled);

        [TestMethod]
        public void EveryEnumMember_HasWireValue()
        {
            foreach (RefreshReason reason in Enum.GetValues(typeof(RefreshReason)))
            {
                Assert.IsTrue(
                    RefreshReasonExtensions.WireValues.ContainsKey(reason),
                    $"RefreshReason.{reason} has no entry in RefreshReasonExtensions.WireValues.");
            }
        }

        [TestMethod]
        public void EveryWireValue_MatchesRegex()
        {
            foreach (KeyValuePair<RefreshReason, string> kvp in RefreshReasonExtensions.WireValues)
            {
                Assert.IsTrue(
                    WireValueRegex.IsMatch(kvp.Value),
                    $"Wire value '{kvp.Value}' (for RefreshReason.{kvp.Key}) violates the required shape [A-Za-z0-9_]+(\\.[A-Za-z0-9_]+)?.");
            }
        }

        [TestMethod]
        public void EveryWireValue_IsUnique()
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<RefreshReason, string> kvp in RefreshReasonExtensions.WireValues)
            {
                Assert.IsTrue(
                    seen.Add(kvp.Value),
                    $"Duplicate wire value '{kvp.Value}' (seen twice, latest at RefreshReason.{kvp.Key}).");
            }
        }

        [TestMethod]
        public void ToHeaderValue_ReturnsRegisteredString()
        {
            Assert.AreEqual("unspecified", RefreshReason.Unspecified.ToHeaderValue());
            Assert.AreEqual("gone.server", RefreshReason.GoneServer.ToHeaderValue());
            Assert.AreEqual("gone.completing_split", RefreshReason.GoneCompletingSplit.ToHeaderValue());
            Assert.AreEqual("gone.connect", RefreshReason.GoneConnect.ToHeaderValue());
            Assert.AreEqual("gone.unknown", RefreshReason.GoneUnknown.ToHeaderValue());
            Assert.AreEqual("gone.write_not_sent", RefreshReason.GoneWriteNotSent.ToHeaderValue());
            Assert.AreEqual("InsufficientReplicas.Quorum", RefreshReason.InsufficientReplicasQuorum.ToHeaderValue());
            Assert.AreEqual("InsufficientReplicas.SuboptimalTimer", RefreshReason.InsufficientReplicasSuboptimalTimer.ToHeaderValue());
            Assert.AreEqual("ReplicaHealth.unhealthyLongLived", RefreshReason.ReplicaHealthUnhealthyLongLived.ToHeaderValue());
            Assert.AreEqual("connection_event.server_closed", RefreshReason.ConnectionEventServerClosed.ToHeaderValue());
        }

        [TestMethod]
        public void ToHeaderValue_UnknownEnumValue_Throws()
        {
            RefreshReason bogus = (RefreshReason)int.MinValue;
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => bogus.ToHeaderValue());
        }

        /// <summary>
        /// Exhaustive coverage: every declared <see cref="TransportErrorCode"/>
        /// must map to a non-<see cref="RefreshReason.Unspecified"/> value.
        /// This fails (in CI) if an upstream TransportErrorCode is added
        /// without updating the switch in FromTransportErrorCode.
        /// </summary>
        [TestMethod]
        public void FromTransportErrorCode_CoversEveryCode()
        {
            foreach (TransportErrorCode code in Enum.GetValues(typeof(TransportErrorCode)))
            {
                RefreshReason mapped = RefreshReasonExtensions.FromTransportErrorCode(code);
                Assert.AreNotEqual(
                    RefreshReason.Unspecified,
                    mapped,
                    $"TransportErrorCode.{code} maps to RefreshReason.Unspecified. Every code must map to a specific reason.");
            }
        }

        [TestMethod]
        public void FromTransportErrorCode_GenericCodes_FoldIntoGoneUnknown()
        {
            Assert.AreEqual(RefreshReason.GoneUnknown, RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.Unknown));
            Assert.AreEqual(RefreshReason.GoneUnknown, RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ChannelOpenFailed));
            Assert.AreEqual(RefreshReason.GoneUnknown, RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ChannelOpenTimeout));
            Assert.AreEqual(RefreshReason.GoneUnknown, RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.RequestTimeout));
        }

        [TestMethod]
        public void FromTransportErrorCode_FailedAndTimeoutPairs_MapToSameReason()
        {
            Assert.AreEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.DnsResolutionFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.DnsResolutionTimeout));
            Assert.AreEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ConnectFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ConnectTimeout));
            Assert.AreEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SslNegotiationFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SslNegotiationTimeout));
            Assert.AreEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SendFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SendTimeout));
            Assert.AreEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ReceiveFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ReceiveTimeout));
        }

        [TestMethod]
        public void FromTransportErrorCode_SendLockTimeout_IsOwnBucket()
        {
            // SendLockTimeout is *client-side lock contention*, not a network send failure.
            Assert.AreEqual(
                RefreshReason.GoneSendLockTimeout,
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SendLockTimeout));
            Assert.AreNotEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SendFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.SendLockTimeout));
        }

        [TestMethod]
        public void FromTransportErrorCode_ReceiveStreamClosed_IsOwnBucket()
        {
            // Server-initiated clean close is distinct from ReceiveFailed/Timeout.
            Assert.AreEqual(
                RefreshReason.GoneReceiveStreamClosed,
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ReceiveStreamClosed));
            Assert.AreNotEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ReceiveFailed),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ReceiveStreamClosed));
        }

        [TestMethod]
        public void FromTransportErrorCode_ChannelWaitingToOpenTimeout_IsOwnBucket()
        {
            // Slot-wait timeout, distinct from ChannelOpenTimeout.
            Assert.AreEqual(
                RefreshReason.GoneChannelWaitingToOpenTimeout,
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ChannelWaitingToOpenTimeout));
            Assert.AreNotEqual(
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ChannelOpenTimeout),
                RefreshReasonExtensions.FromTransportErrorCode(TransportErrorCode.ChannelWaitingToOpenTimeout));
        }

        [TestMethod]
        public void EnumCount_MatchesWireValuesCount()
        {
            int enumCount = Enum.GetValues(typeof(RefreshReason)).Length;
            Assert.AreEqual(
                enumCount,
                RefreshReasonExtensions.WireValues.Count,
                "Every RefreshReason member must have exactly one entry in WireValues (no orphans, no extras).");
        }
    }
}
