//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test cases for Batch. More test cases to come after hybrid row support is enabled
    /// </summary>
    [TestClass]
    public class BatchUnitTests
    {
        /// <summary>
        /// Test to make sure IsFeedRequest is true for Batch operation
        /// </summary>
        [TestMethod]
        public void TestIsFeedRequestForBatchOperation()
        {
            Assert.IsTrue(GatewayStoreClient.IsFeedRequest(OperationType.Batch));
        }

        /// <summary>
        /// Test to make sure IsWriteOperation is true for batch operation
        /// </summary>
        [TestMethod]
        public void BatchIsWriteOperation()
        {
            Assert.IsTrue(OperationType.Batch.IsWriteOperation());
        }
    }
}