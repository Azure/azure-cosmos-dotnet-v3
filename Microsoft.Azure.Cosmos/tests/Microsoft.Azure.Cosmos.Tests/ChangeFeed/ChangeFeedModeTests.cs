//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedModeTests
    {
        [TestMethod]
        public void ChangeFeedMode_Incremental_SetsCorrectHeader()
        {
            ChangeFeedMode changeFeedMode = ChangeFeedMode.Incremental();
            RequestMessage requestMessage = new RequestMessage();
            changeFeedMode.Accept(requestMessage);

            Assert.AreEqual(HttpConstants.A_IMHeaderValues.IncrementalFeed, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
        }

        [TestMethod]
        public void ChangeFeedMode_FullFidelity_SetsCorrectHeader()
        {
            ChangeFeedMode changeFeedMode = ChangeFeedMode.FullFidelity();
            RequestMessage requestMessage = new RequestMessage();
            changeFeedMode.Accept(requestMessage);

            Assert.AreEqual(ChangeFeedModeFullFidelity.FullFidelityHeader, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
        }
    }
}
