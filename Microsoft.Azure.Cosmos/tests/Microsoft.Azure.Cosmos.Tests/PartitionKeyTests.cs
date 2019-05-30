//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class PartitionKeyTests
    {
        [ExpectedException(typeof(ArgumentNullException))]
        [TestMethod]
        public void NullValue()
        {
            new PartitionKey(null);
        }

        [TestMethod]
        public void PartitionKeyNone()
        {
            PartitionKey pk = new PartitionKey(CosmosContainerSettings.NonePartitionKeyValue);
            Assert.AreEqual(new Documents.PartitionKey(CosmosContainerSettings.NonePartitionKeyValue).InternalKey.ToJsonString(), pk.ToString());
        }
    }
}