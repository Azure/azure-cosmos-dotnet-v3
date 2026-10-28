//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using System;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TimeoutHelperTests
    {
        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowTimeoutIfElapsed_WhenNotElapsed_DoesNotThrow()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.FromHours(1));

            helper.ThrowTimeoutIfElapsed(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowTimeoutIfElapsed_WhenElapsed_ThrowsWithDefaultUnknownSubStatus()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.Zero);

            RequestTimeoutException ex = Assert.ThrowsException<RequestTimeoutException>(
                () => helper.ThrowTimeoutIfElapsed());

            Assert.AreEqual(SubStatusCodes.Unknown, ex.GetSubStatus());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowTimeoutIfElapsed_WhenElapsed_GlobalStrongWriteBarrierSubStatus()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.Zero);

            RequestTimeoutException ex = Assert.ThrowsException<RequestTimeoutException>(
                () => helper.ThrowTimeoutIfElapsed(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet));

            Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowGoneIfElapsed_WhenNotElapsed_DoesNotThrow()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.FromHours(1));

            // Should not throw when plenty of time remains.
            helper.ThrowGoneIfElapsed(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet);
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowGoneIfElapsed_WhenElapsed_DefaultSubStatusIsTimeoutGenerated410()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.Zero);

            GoneException ex = Assert.ThrowsException<GoneException>(
                () => helper.ThrowGoneIfElapsed());

            Assert.AreEqual(SubStatusCodes.TimeoutGenerated410, ex.GetSubStatus());
        }

        [TestMethod]
        [Owner("cosmosdbcomputeteam")]
        public void ThrowGoneIfElapsed_WhenElapsed_CustomSubStatus()
        {
            TimeoutHelper helper = new TimeoutHelper(TimeSpan.Zero);

            GoneException ex = Assert.ThrowsException<GoneException>(
                () => helper.ThrowGoneIfElapsed(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet));

            Assert.AreEqual(SubStatusCodes.Server_GlobalStrongWriteBarrierNotMet, ex.GetSubStatus());
        }
    }
}