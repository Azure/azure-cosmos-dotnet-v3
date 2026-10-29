//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HttpTimeoutPolicyReducedRetriesTests
    {
        [TestMethod]
        public void ReducedPolicyHasFiveAndSixtyFiveSecondAttempts()
        {
            HttpTimeoutPolicy policy = HttpTimeoutPolicyControlPlaneReducedRetriesHotPath.InstanceShouldThrow503OnTimeout;
            List<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> attempts = new List<(TimeSpan, TimeSpan)>();
            IEnumerator<(TimeSpan requestTimeout, TimeSpan delayForNextRequest)> enumerator = policy.GetTimeoutEnumerator();
            while (enumerator.MoveNext())
            {
                attempts.Add(enumerator.Current);
            }

            Assert.AreEqual(2, attempts.Count);
            Assert.AreEqual(TimeSpan.FromSeconds(5), attempts[0].requestTimeout);
            Assert.AreEqual(TimeSpan.FromSeconds(1), attempts[0].delayForNextRequest);
            Assert.AreEqual(TimeSpan.FromSeconds(65), attempts[1].requestTimeout);
            Assert.AreEqual(TimeSpan.Zero, attempts[1].delayForNextRequest);
            Assert.IsTrue(policy.ShouldThrow503OnTimeout);
        }

        [TestMethod]
        public void FlagSelectsReducedPolicyForControlPlaneRequests()
        {
            using DocumentServiceRequest addressRequest = DocumentServiceRequest.Create(
                OperationType.Read,
                ResourceType.Address,
                AuthorizationTokenType.PrimaryMasterKey);

            Assert.IsInstanceOfType(
                HttpTimeoutPolicy.GetTimeoutPolicy(addressRequest, useReducedControlPlaneRetries: true),
                typeof(HttpTimeoutPolicyControlPlaneReducedRetriesHotPath));
            Assert.IsInstanceOfType(
                HttpTimeoutPolicy.GetTimeoutPolicy(addressRequest),
                typeof(HttpTimeoutPolicyControlPlaneRetriableHotPath));
        }

        [TestMethod]
        public void FlagDoesNotAffectDataPlaneRequests()
        {
            using DocumentServiceRequest writeRequest = DocumentServiceRequest.Create(
                OperationType.Create,
                ResourceType.Document,
                AuthorizationTokenType.PrimaryMasterKey);

            Assert.IsInstanceOfType(
                HttpTimeoutPolicy.GetTimeoutPolicy(writeRequest, useReducedControlPlaneRetries: true),
                typeof(HttpTimeoutPolicyDefault));
        }

        [TestMethod]
        public void ConnectionPolicyFlowsFromClientOptions()
        {
            CosmosClientOptions options = new CosmosClientOptions { UseReducedControlPlaneRetries = true };
            Assert.IsTrue(options.GetConnectionPolicy(clientId: 1).UseReducedControlPlaneRetries);
            Assert.IsFalse(new CosmosClientOptions().GetConnectionPolicy(clientId: 1).UseReducedControlPlaneRetries);
        }
    }
}
