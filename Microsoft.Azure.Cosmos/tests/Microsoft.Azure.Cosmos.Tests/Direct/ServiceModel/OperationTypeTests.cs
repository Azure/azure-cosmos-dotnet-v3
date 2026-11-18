//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Direct.Test
{
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OperationTypeTests
    {
        [TestMethod]
        [Owner("kirankk")]
        public void BatchIsWriteOperation()
        {
            Assert.IsTrue(OperationType.Batch.IsWriteOperation());
        }

        [TestMethod]
        [Owner("paulchen")]
        public void CompleteTransactionIsWriteOperation()
        {
            Assert.IsTrue(OperationType.CompleteUserTransaction.IsWriteOperation());
        }
    }
}