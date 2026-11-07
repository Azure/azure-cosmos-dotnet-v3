//------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseManagerTests
    {
        [DataTestMethod]
        [DataRow(null, null, true)]
        [DataRow("fingerprint", "fingerprint", true)]
        [DataRow(null, "fingerprint", false)]
        [DataRow("fingerprint", null, false)]
        [DataRow("fingerprint", "different", false)]
        [DataRow("fingerprint", "FINGERPRINT", false)]
        [DataRow(null, "", false)]
        public void QueryCompatibilityUsesOrdinalEquality(string configuredHash, string persistedHash, bool compatible)
        {
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            manager.ConfigureQueryFilter(configuredHash);
            DocumentServiceLease lease = new DocumentServiceLeaseCore { QueryFilterHash = persistedHash };

            if (compatible)
            {
                manager.VerifyQueryFilter(lease);
            }
            else
            {
                Assert.ThrowsException<ArgumentException>(() => manager.VerifyQueryFilter(lease));
            }

            Assert.AreEqual(persistedHash, lease.QueryFilterHash);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("fingerprint")]
        public void RepeatingTheSameQueryConfigurationIsAllowed(string hash)
        {
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            manager.ConfigureQueryFilter(hash);
            manager.ConfigureQueryFilter(hash);
            manager.VerifyQueryFilter(new DocumentServiceLeaseCore { QueryFilterHash = hash });
        }

        [DataTestMethod]
        [DataRow(null, "fingerprint")]
        [DataRow("fingerprint", null)]
        [DataRow("fingerprint", "different")]
        [DataRow("fingerprint", "FINGERPRINT")]
        public void DifferentQueryConfigurationIsRejectedWithoutChangingIdentity(string originalHash, string replacementHash)
        {
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            manager.ConfigureQueryFilter(originalHash);

            Assert.ThrowsException<ArgumentException>(() => manager.ConfigureQueryFilter(replacementHash));

            manager.VerifyQueryFilter(new DocumentServiceLeaseCore { QueryFilterHash = originalHash });
        }

        [TestMethod]
        public void UnconfiguredManagerAcceptsLegacyLease()
        {
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            manager.VerifyQueryFilter(new DocumentServiceLeaseCore());
        }

        [TestMethod]
        public void NullLeaseIsRejected()
        {
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            Assert.AreEqual("lease", Assert.ThrowsException<ArgumentNullException>(
                () => manager.VerifyQueryFilter(null)).ParamName);
        }

        [TestMethod]
        public void MismatchesDoNotExposeSqlOrHashesOrModifyLeaseState()
        {
            const string originalQuery = "SELECT * FROM c WHERE c.state = \"WA\"";
            const string differentQuery = "SELECT * FROM c WHERE c.state = \"OR\"";
            string originalHash = ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(originalQuery));
            string differentHash = ChangeFeedHelper.GetQueryFilterHash(new ChangeFeedQuerySpec(differentQuery));
            DocumentServiceLeaseManager manager = Mock.Of<DocumentServiceLeaseManager>();
            manager.ConfigureQueryFilter(originalHash);
            DocumentServiceLease lease = new DocumentServiceLeaseCore
            {
                QueryFilterHash = differentHash,
                Owner = "owner",
                ContinuationToken = "continuation"
            };

            ArgumentException configurationError = Assert.ThrowsException<ArgumentException>(
                () => manager.ConfigureQueryFilter(differentHash));
            ArgumentException leaseError = Assert.ThrowsException<ArgumentException>(
                () => manager.VerifyQueryFilter(lease));

            foreach (string value in new[] { originalQuery, differentQuery, originalHash, differentHash })
            {
                Assert.IsFalse(configurationError.Message.Contains(value));
                Assert.IsFalse(leaseError.Message.Contains(value));
            }

            Assert.AreEqual(differentHash, lease.QueryFilterHash);
            Assert.AreEqual("owner", lease.Owner);
            Assert.AreEqual("continuation", lease.ContinuationToken);
        }
    }
}
