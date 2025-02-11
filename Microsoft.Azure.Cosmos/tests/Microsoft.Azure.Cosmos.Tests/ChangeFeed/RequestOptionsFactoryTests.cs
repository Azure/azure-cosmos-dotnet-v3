//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class RequestOptionsFactoryTests
    {
        private const string IdValue = "anId";
        private const string PartitionKeyValue = "aPartitionKey";

        [TestMethod]
        public void SinglePartitionRequestOptionsFactory_GetPartitionKey()
        {
            SinglePartitionRequestOptionsFactory factory = new SinglePartitionRequestOptionsFactory();
            Assert.AreEqual(PartitionKey.None, factory.GetPartitionKey(IdValue, PartitionKeyValue));
        }

        [TestMethod]
        public void PartitionedByIdCollectionRequestOptionsFactory_GetPartitionKey()
        {
            PartitionedByIdCollectionRequestOptionsFactory factory = new PartitionedByIdCollectionRequestOptionsFactory();
            Assert.AreEqual(new PartitionKey(IdValue), factory.GetPartitionKey(IdValue, PartitionKeyValue));
        }

        [TestMethod]
        public void PartitionedByPartitionKeyCollectionRequestOptionsFactory_GetPartitionKey()
        {
            PartitionedByPartitionKeyCollectionRequestOptionsFactory factory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            Assert.AreEqual(new PartitionKey(PartitionKeyValue), factory.GetPartitionKey(IdValue, PartitionKeyValue));
        }

        [TestMethod]
        public void SinglePartitionRequestOptionsFactory_AddPartitionKeyIfNeeded()
        {
            bool invoked = false;
            void action(string pk)
            {
                invoked = true;
                Assert.Fail("Should not invoke");
            }

            SinglePartitionRequestOptionsFactory factory = new SinglePartitionRequestOptionsFactory();
            factory.AddPartitionKeyIfNeeded(action, PartitionKeyValue);
            Assert.IsFalse(invoked);
        }

        [TestMethod]
        public void PartitionedByIdCollectionRequestOptionsFactory_AddPartitionKeyIfNeeded()
        {
            bool invoked = false;
            void action(string pk)
            {
                invoked = true;
                Assert.Fail("Should not invoke");
            }

            PartitionedByIdCollectionRequestOptionsFactory factory = new PartitionedByIdCollectionRequestOptionsFactory();
            factory.AddPartitionKeyIfNeeded(action, PartitionKeyValue);
            Assert.IsFalse(invoked);
        }

        [TestMethod]
        public void PartitionedByPartitionKeyCollectionRequestOptionsFactory_AddPartitionKeyIfNeeded()
        {
            bool invoked = false;
            void action(string pk)
            {
                invoked = true;
                Assert.AreEqual(PartitionKeyValue, pk); ;
            }

            PartitionedByPartitionKeyCollectionRequestOptionsFactory factory = new PartitionedByPartitionKeyCollectionRequestOptionsFactory();
            factory.AddPartitionKeyIfNeeded(action, PartitionKeyValue);
            Assert.IsTrue(invoked);
        }
    }
}