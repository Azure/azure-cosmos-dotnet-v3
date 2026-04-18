//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Routing
{
    using System;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="GatewayAddressCache.EmitRefreshReasonHeader"/>:
    /// verifies precedence (explicit &gt; request context &gt; default), header
    /// emission, and the opt-in validator invariant that guards untagged
    /// force-refresh sites.
    /// </summary>
    [TestClass]
    public class RefreshReasonEmissionTests
    {
        [TestMethod]
        public void Emit_ExplicitReason_WinsOverRequestContext()
        {
            INameValueCollection headers = new RequestNameValueCollection();
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.RefreshReason = RefreshReason.GoneServer;

            GatewayAddressCache.EmitRefreshReasonHeader(
                headers: headers,
                request: request,
                explicitReason: RefreshReason.ReplicaHealthUnhealthyLongLived,
                callerName: nameof(this.Emit_ExplicitReason_WinsOverRequestContext));

            Assert.AreEqual(
                "ReplicaHealth.unhealthyLongLived",
                headers.Get(HttpConstants.HttpHeaders.CosmosRefreshReason));
        }

        [TestMethod]
        public void Emit_NoExplicit_UsesRequestContextReason()
        {
            INameValueCollection headers = new RequestNameValueCollection();
            using DocumentServiceRequest request = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);
            request.RequestContext.RefreshReason = RefreshReason.GoneConnect;

            GatewayAddressCache.EmitRefreshReasonHeader(
                headers: headers,
                request: request,
                explicitReason: RefreshReason.Unspecified,
                callerName: nameof(this.Emit_NoExplicit_UsesRequestContextReason));

            Assert.AreEqual(
                "gone.connect",
                headers.Get(HttpConstants.HttpHeaders.CosmosRefreshReason));
        }

        [TestMethod]
        public void Emit_NullRequest_ExplicitReasonWrites()
        {
            // The on-demand unhealthy-URI refresh path calls with request=null.
            INameValueCollection headers = new RequestNameValueCollection();

            GatewayAddressCache.EmitRefreshReasonHeader(
                headers: headers,
                request: null,
                explicitReason: RefreshReason.ReplicaHealthUnhealthyLongLived,
                callerName: nameof(this.Emit_NullRequest_ExplicitReasonWrites));

            Assert.AreEqual(
                "ReplicaHealth.unhealthyLongLived",
                headers.Get(HttpConstants.HttpHeaders.CosmosRefreshReason));
        }

        [TestMethod]
        public void Emit_BothUnspecified_ValidatorOff_NoHeader()
        {
            // Default behavior: when nothing is tagged, no header is written
            // (zero production overhead, graceful degradation).
            bool previous = GatewayAddressCache.ValidateRefreshReasonPresence;
            try
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = false;
                INameValueCollection headers = new RequestNameValueCollection();
                using DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Read,
                    ResourceType.Document,
                    AuthorizationTokenType.PrimaryMasterKey);

                GatewayAddressCache.EmitRefreshReasonHeader(
                    headers: headers,
                    request: request,
                    explicitReason: RefreshReason.Unspecified,
                    callerName: nameof(this.Emit_BothUnspecified_ValidatorOff_NoHeader));

                Assert.IsNull(headers.Get(HttpConstants.HttpHeaders.CosmosRefreshReason));
            }
            finally
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = previous;
            }
        }

        [TestMethod]
        public void Emit_BothUnspecified_ValidatorOn_Throws()
        {
            // Opt-in invariant: any forced address-cache refresh without a
            // tagged reason must throw, so new untagged call sites are caught
            // automatically in CI.
            bool previous = GatewayAddressCache.ValidateRefreshReasonPresence;
            try
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = true;
                INameValueCollection headers = new RequestNameValueCollection();
                using DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Read,
                    ResourceType.Document,
                    AuthorizationTokenType.PrimaryMasterKey);

                InvalidOperationException ex = Assert.ThrowsException<InvalidOperationException>(
                    () => GatewayAddressCache.EmitRefreshReasonHeader(
                        headers: headers,
                        request: request,
                        explicitReason: RefreshReason.Unspecified,
                        callerName: "TestCaller"));

                StringAssert.Contains(ex.Message, "TestCaller");
                StringAssert.Contains(ex.Message, "RefreshReason");
            }
            finally
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = previous;
            }
        }

        [TestMethod]
        public void Emit_ValidatorOn_ExplicitReasonSet_DoesNotThrow()
        {
            bool previous = GatewayAddressCache.ValidateRefreshReasonPresence;
            try
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = true;
                INameValueCollection headers = new RequestNameValueCollection();
                using DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.Read,
                    ResourceType.Document,
                    AuthorizationTokenType.PrimaryMasterKey);

                GatewayAddressCache.EmitRefreshReasonHeader(
                    headers: headers,
                    request: request,
                    explicitReason: RefreshReason.InsufficientReplicasSuboptimalTimer,
                    callerName: nameof(this.Emit_ValidatorOn_ExplicitReasonSet_DoesNotThrow));

                Assert.AreEqual(
                    "InsufficientReplicas.SuboptimalTimer",
                    headers.Get(HttpConstants.HttpHeaders.CosmosRefreshReason));
            }
            finally
            {
                GatewayAddressCache.ValidateRefreshReasonPresence = previous;
            }
        }
    }
}
