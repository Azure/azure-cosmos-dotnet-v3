//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;


    [TestClass]
    [TestCategory("ChangeFeed")]
    public class EqualPartitionsBalancingStrategyTests
    {
        private const string ownerSelf = "self";
        private const string owner1 = "owner 1";
        private const string owner2 = "owner 2";
        private const string ownerNone = null;

        [TestMethod]
        public void CalculateLeasesToTake_NoLeases_ReturnsEmpty()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            IEnumerable<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(Enumerable.Empty<DocumentServiceLease>());
            Assert.IsTrue(leasesToTake.Count() == 0);
        }

        [TestMethod]
        public void CalculateLeasesToTake_OwnLeasesOnly_ReturnsEmpty()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            IEnumerable<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(new[] { this.CreateLease(ownerSelf, "1"), this.CreateLease(ownerSelf, "2") });
            Assert.IsTrue(leasesToTake.Count() == 0);
        }

        [TestMethod]
        public void CalculateLeasesToTake_NotOwnedLeasesOnly_ReturnsAll()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            HashSet<DocumentServiceLease> allLeases = new HashSet<DocumentServiceLease> { this.CreateLease(ownerNone, "1"), this.CreateLease(ownerNone, "2") };
            IEnumerable<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases);
            CollectionAssert.AreEqual(allLeases.ToList(), new HashSet<DocumentServiceLease>(leasesToTake).ToList());
        }

        [TestMethod]
        public void CalculateLeasesToTake_ExpiredLeasesOnly_ReturnsAll()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            HashSet<DocumentServiceLease> allLeases = new HashSet<DocumentServiceLease> { this.CreateExpiredLease(ownerSelf, "1"), this.CreateExpiredLease(owner1, "2") };
            IEnumerable<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases);
            CollectionAssert.AreEqual(allLeases.ToList(), new HashSet<DocumentServiceLease>(leasesToTake).ToList());
        }

        [TestMethod]
        public void CalculateLeasesToTake_OtherSingleOwnerTwoLeasesOnly_ReturnsOne()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            HashSet<DocumentServiceLease> allLeases = new HashSet<DocumentServiceLease> { this.CreateLease(owner1, "1"), this.CreateLease(owner1, "2") };
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 1);
            CollectionAssert.IsSubsetOf(new HashSet<DocumentServiceLease>(leasesToTake).ToList(), allLeases.ToList());
        }

        [TestMethod]
        public void CalculateLeasesToTake_ExpiredAndOtherOwner_ReturnsExpiredOnly()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            DocumentServiceLease expiredLease = this.CreateExpiredLease(owner1, "4");
            HashSet<DocumentServiceLease> allLeases = new HashSet<DocumentServiceLease>
            {
                this.CreateLease(owner1, "1"),
                this.CreateLease(owner1, "2"),
                this.CreateLease(owner1, "3"),
                expiredLease
            };
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 1);
            CollectionAssert.Contains(leasesToTake, expiredLease);
        }

        [TestMethod]
        public void CalculateLeasesToTake_ExpiredAndOtherSingleOwner_ReturnsHalfOfExpiredRoundedUp()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases1 = new List<DocumentServiceLease>
            {
                this.CreateLease(owner1, "0")
            };
            allLeases1.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateExpiredLease(owner1, index.ToString())));
            List<DocumentServiceLease> allLeases = allLeases1;
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.AreEqual(6, leasesToTake.Count);
        }

        [TestMethod]
        public void CalculateLeasesToTake_MinPartitionsSet_ReturnsMinCountOfPartitions()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy(minPartitionCount: 7);
            List<DocumentServiceLease> allLeases1 = new List<DocumentServiceLease>
            {
                this.CreateLease(owner1, "0")
            };
            allLeases1.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateExpiredLease(owner1, index.ToString())));
            List<DocumentServiceLease> allLeases = allLeases1;
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.AreEqual(7, leasesToTake.Count);
        }

        [TestMethod]
        public void CalculateLeasesToTake_MaxPartitionsSet_ReturnsMaxCountOfPartitions()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy(maxPartitionCount: 3);
            List<DocumentServiceLease> allLeases1 = new List<DocumentServiceLease>
            {
                this.CreateLease(owner1, "0")
            };
            allLeases1.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateExpiredLease(owner1, index.ToString())));
            List<DocumentServiceLease> allLeases = allLeases1;
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.AreEqual(3, leasesToTake.Count);
        }

        [TestMethod]
        public void CalculateLeasesToTake_TwoOwners_ReturnsStolenFromLargerOwner()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 5).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateLease(owner2, "B" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 1);
            Assert.IsTrue(leasesToTake.First().CurrentLeaseToken.StartsWith("B"));
        }

        [TestMethod]
        public void CalculateLeasesToTake_HavingMoreThanOtherOwner_ReturnsEmpty()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 5).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 6).Select(index => this.CreateLease(ownerSelf, "B" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 0);
        }

        [TestMethod]
        public void CalculateLeasesToTake_HavingEqualThanOtherOwner_ReturnsEmpty()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 5).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 5).Select(index => this.CreateLease(ownerSelf, "B" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 0);
        }

        [TestMethod]
        public void CalculateLeasesToTake_AllOtherOwnersEqualTargetCount_ReturnsEmpty()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 4).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 3).Select(index => this.CreateLease(ownerSelf, "B" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 0);
        }

        [TestMethod]
        public void CalculateLeasesToTake_OtherOwnerGreaterThanTargetCount_ReturnsLease()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 4).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 2).Select(index => this.CreateLease(ownerSelf, "B" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 1);
            Assert.IsTrue(leasesToTake.First().CurrentLeaseToken.StartsWith("A"));
        }

        [TestMethod]
        public void CalculateLeasesToTake_NeedTwoAndOtherOwnersEqualThanTargetCount_ReturnsLease()
        {
            EqualPartitionsBalancingStrategy strategy = this.CreateStrategy();
            List<DocumentServiceLease> allLeases = new List<DocumentServiceLease>();
            allLeases.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateLease(owner1, "A" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 10).Select(index => this.CreateLease(owner2, "B" + index.ToString())));
            allLeases.AddRange(Enumerable.Range(1, 8).Select(index => this.CreateLease(ownerSelf, "C" + index.ToString())));
            List<DocumentServiceLease> leasesToTake = strategy.SelectLeasesToTake(allLeases).ToList();
            Assert.IsTrue(leasesToTake.Count == 1);
        }

        private EqualPartitionsBalancingStrategy CreateStrategy(int minPartitionCount = 0, int maxPartitionCount = 0)
        {
            TimeSpan leaseExpirationInterval = TimeSpan.FromMinutes(10);
            return new EqualPartitionsBalancingStrategy(ownerSelf, minPartitionCount, maxPartitionCount, leaseExpirationInterval);
        }

        private DocumentServiceLease CreateLease(string owner, string partitionId)
        {
            return CreateLease(owner, partitionId, DateTime.UtcNow);
        }

        private DocumentServiceLease CreateExpiredLease(string owner, string partitionId)
        {
            return CreateLease(owner, partitionId, DateTime.UtcNow.AddYears(-1));
        }

        private static DocumentServiceLease CreateLease(string owner, string partitionId, DateTime timestamp)
        {
            DocumentServiceLease lease = Mock.Of<DocumentServiceLease>();
            Mock.Get(lease).Setup(l => l.Owner).Returns(owner);
            Mock.Get(lease).Setup(l => l.CurrentLeaseToken).Returns(partitionId);
            Mock.Get(lease).Setup(l => l.Timestamp).Returns(timestamp);
            return lease;
        }
    }
}