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
    }
}
