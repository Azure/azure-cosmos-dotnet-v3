//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Diagnostics
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class RequestedRegionTests
    {
        [TestMethod]
        public void Constructor_StoresPropertiesVerbatim()
        {
            RequestedRegion r = new RequestedRegion("East US", RequestedRegionReason.Hedging);

            Assert.AreEqual("East US", r.RegionName);
            Assert.AreEqual(RequestedRegionReason.Hedging, r.Reason);
        }

        [TestMethod]
        public void Constructor_NullRegionName_Throws()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => new RequestedRegion(null, RequestedRegionReason.Initial));
        }

        [TestMethod]
        public void Equality_RegionNameIsCaseInsensitive()
        {
            RequestedRegion a = new RequestedRegion("East US", RequestedRegionReason.Initial);
            RequestedRegion b = new RequestedRegion("east us", RequestedRegionReason.Initial);

            Assert.IsTrue(a.Equals(b));
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);
            Assert.IsTrue(a.Equals((object)b));
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [TestMethod]
        public void Equality_DifferentReason_NotEqual()
        {
            RequestedRegion a = new RequestedRegion("East US", RequestedRegionReason.Initial);
            RequestedRegion b = new RequestedRegion("East US", RequestedRegionReason.Hedging);

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a != b);
            Assert.IsFalse(a == b);
        }

        [TestMethod]
        public void Equals_NonRequestedRegionObject_ReturnsFalse()
        {
            RequestedRegion r = new RequestedRegion("East US", RequestedRegionReason.Initial);
            Assert.IsFalse(r.Equals("East US"));
            Assert.IsFalse(r.Equals(null));
        }

        [TestMethod]
        public void ToString_FormatsAsRegionColonReason()
        {
            RequestedRegion r = new RequestedRegion("East US", RequestedRegionReason.Hedging);
            Assert.AreEqual("East US:Hedging", r.ToString());
        }

        [TestMethod]
        public void DefaultStruct_ReasonIsUnknownSentinel_NotInitial()
        {
            // Guards against the pre-GA enum layout where Initial = 0 collided with
            // default(RequestedRegion). The Unknown sentinel must occupy the underlying-zero
            // slot so that struct-default-constructed values (e.g. new RequestedRegion[N],
            // uninitialized fields, deserialized values where Reason was absent) are
            // distinguishable from a real first dispatch.
            RequestedRegion defaultRegion = default;

            Assert.AreEqual(RequestedRegionReason.Unknown, defaultRegion.Reason);
            Assert.AreNotEqual(RequestedRegionReason.Initial, defaultRegion.Reason);
            Assert.AreEqual(0, (byte)RequestedRegionReason.Unknown);
            Assert.IsNull(defaultRegion.RegionName);
        }

        [TestMethod]
        public void Enum_UnknownIsZeroAndInitialIsOne()
        {
            // Pin the underlying-byte values so any future renumbering is caught.
            Assert.AreEqual(0, (byte)RequestedRegionReason.Unknown);
            Assert.AreEqual(1, (byte)RequestedRegionReason.Initial);
            Assert.AreEqual(2, (byte)RequestedRegionReason.OperationRetry);
            Assert.AreEqual(3, (byte)RequestedRegionReason.TransportRetry);
            Assert.AreEqual(4, (byte)RequestedRegionReason.Hedging);
            Assert.AreEqual(5, (byte)RequestedRegionReason.RegionFailover);
            Assert.AreEqual(6, (byte)RequestedRegionReason.CircuitBreakerProbe);
        }
    }
}
