//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class CheckpointerObserverFactoryTests
    {
        [TestMethod]
        public void WhenNotUsingManualCheckpoint()
        {
            Mock<ChangeFeedObserverFactory> mockedFactory = new Mock<ChangeFeedObserverFactory>();
            mockedFactory.Setup(f => f.CreateObserver()).Returns(Mock.Of<ChangeFeedObserver>());
            CheckpointerObserverFactory factory = new CheckpointerObserverFactory(mockedFactory.Object, withManualCheckpointing: false);

            ChangeFeedObserver observer = factory.CreateObserver();
            Assert.IsInstanceOfType(observer, typeof(AutoCheckpointer));
        }

        [TestMethod]
        public void WhenUsingManualCheckpoint()
        {
            Mock<ChangeFeedObserverFactory> mockedFactory = new Mock<ChangeFeedObserverFactory>();
            mockedFactory.Setup(f => f.CreateObserver()).Returns(Mock.Of<ChangeFeedObserver>());
            CheckpointerObserverFactory factory = new CheckpointerObserverFactory(mockedFactory.Object, withManualCheckpointing: true);

            ChangeFeedObserver observer = factory.CreateObserver();
            Assert.IsInstanceOfType(observer, typeof(ObserverExceptionWrappingChangeFeedObserverDecorator));
        }
    }
}