//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ValidationHelpersTest
    {
        [TestMethod]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.Strong)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong)]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness)]
        [DataRow(false, ConsistencyLevel.Session, ConsistencyLevel.Strong)]
        [DataRow(false, ConsistencyLevel.BoundedStaleness, ConsistencyLevel.Strong)]
        public void TestIsValidConsistencyLevelOverwrite(bool isValidConsistencyLevelOverwrite,
            ConsistencyLevel backendConsistencyLevel,
            ConsistencyLevel desiredConsistencyLevel)
        {
            bool result = ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistencyLevel,
                desiredConsistencyLevel,
                true,
                Documents.OperationType.Read,
                Documents.ResourceType.Document);

            Assert.AreEqual(isValidConsistencyLevelOverwrite, result);
        }

        [TestMethod]
        [DataRow(false, ConsistencyLevel.Eventual, ConsistencyLevel.Strong)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong)]
        [DataRow(false, ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness)]
        public void TestIsValidConsistencyLevelOverwrite_OnlyWhenSpecifyingExplicitOverwrite(bool isValidConsistencyLevelOverwrite,
            ConsistencyLevel backendConsistencyLevel,
            ConsistencyLevel desiredConsistencyLevel)
        {
            bool result = ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistencyLevel,
                desiredConsistencyLevel,
                false,
                Documents.OperationType.Read,
                Documents.ResourceType.Document);

            Assert.AreEqual(isValidConsistencyLevelOverwrite, result);
        }

        [TestMethod]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.Strong, Documents.OperationType.Read)]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.Strong, Documents.OperationType.ReadFeed)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong, Documents.OperationType.Query)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong, Documents.OperationType.SqlQuery)]
        [DataRow(false, ConsistencyLevel.Eventual, ConsistencyLevel.Strong, Documents.OperationType.QueryPlan)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong, Documents.OperationType.Create)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.Strong, Documents.OperationType.Batch)]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness, Documents.OperationType.Read)]
        [DataRow(true, ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness, Documents.OperationType.ReadFeed)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness, Documents.OperationType.Query)]
        [DataRow(true, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness, Documents.OperationType.SqlQuery)]
        [DataRow(false, ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness, Documents.OperationType.QueryPlan)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness, Documents.OperationType.Create)]
        [DataRow(false, ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness, Documents.OperationType.Batch)]
        public void TestIsValidConsistencyLevelOverwrite_OnlyAllowedForCertainOperationTypes(
            bool isValidConsistencyLevelOverwrite,
            ConsistencyLevel backendConsistencyLevel,
            ConsistencyLevel desiredConsistencyLevel,
            dynamic operationType)
        {
            bool result = ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistencyLevel,
                desiredConsistencyLevel,
                true,
                (Documents.OperationType) operationType,
                Documents.ResourceType.Document);

            Assert.AreEqual(isValidConsistencyLevelOverwrite, result);
        }
    }
}