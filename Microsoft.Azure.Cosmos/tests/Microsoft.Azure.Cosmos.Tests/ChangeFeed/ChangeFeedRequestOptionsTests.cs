//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class ChangeFeedRequestOptionsTests
    {
        [TestMethod]
        public void ChangeFeedMode_Incremental_SetsCorrectHeader()
        {
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions
            {
                FeedMode = ChangeFeedMode.Incremental()
            };
            RequestMessage requestMessage = new RequestMessage();
            changeFeedRequestOptions.PopulateRequestOptions(requestMessage);

            Assert.AreEqual(HttpConstants.A_IMHeaderValues.IncrementalFeed, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
        }

        [TestMethod]
        public void ChangeFeedMode_FullFidelity_SetsCorrectHeader()
        {
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions
            {
                FeedMode = ChangeFeedMode.FullFidelity()
            };
            RequestMessage requestMessage = new RequestMessage();
            changeFeedRequestOptions.PopulateRequestOptions(requestMessage);

            Assert.AreEqual(ChangeFeedModeFullFidelity.FullFidelityHeader, requestMessage.Headers[HttpConstants.HttpHeaders.A_IM]);
        }

        [TestMethod]
        public void ChangeFeedMode_Default()
        {
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions();

            Assert.AreEqual(ChangeFeedMode.Incremental(), changeFeedRequestOptions.FeedMode);
        }

        [TestMethod]
        public void ChangeFeedRequestOptions_Clone()
        {
            ChangeFeedRequestOptions changeFeedRequestOptions = new ChangeFeedRequestOptions()
            {
                FeedMode = ChangeFeedMode.FullFidelity(),
                PageSizeHint = 20
            };

            ChangeFeedRequestOptions changeFeedRequestOptions_Clone = changeFeedRequestOptions.Clone();

            Assert.AreEqual(changeFeedRequestOptions.FeedMode, changeFeedRequestOptions_Clone.FeedMode);
            Assert.AreEqual(changeFeedRequestOptions.PageSizeHint, changeFeedRequestOptions_Clone.PageSizeHint);
        }
    }
}
