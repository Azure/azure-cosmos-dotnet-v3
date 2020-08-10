//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using Microsoft.Azure.Cosmos.ChangeFeed.Configuration;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedProcessing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class CheckpointerObserverFactoryTests
    {
        [TestMethod]
        public void CheckpointerObserverFactory_DefaultIsAutomatic()
        {
            CheckpointerObserverFactory<dynamic> factory = new CheckpointerObserverFactory<dynamic>(Mock.Of<ChangeFeedObserverFactory<dynamic>>(), new CheckpointFrequency());
            ChangeFeedObserver<dynamic> createdObserver = factory.CreateObserver();
            Assert.IsInstanceOfType(createdObserver, typeof(AutoCheckpointer<dynamic>));
        }

        [TestMethod]
        public void CheckpointerObserverFactory_WhenManual()
        {
            CheckpointerObserverFactory<dynamic> factory = new CheckpointerObserverFactory<dynamic>(Mock.Of<ChangeFeedObserverFactory<dynamic>>(), new CheckpointFrequency() { ExplicitCheckpoint = true });
            ChangeFeedObserver<dynamic> createdObserver = factory.CreateObserver();
            Assert.IsInstanceOfType(createdObserver, typeof(ObserverExceptionWrappingChangeFeedObserverDecorator<dynamic>));
        }
    }
}
