//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ValidationHelpersTest
    {
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void TestIsValidConsistencyLevelOverwrite_AllCombinations(
            bool isValidConsistencyLevelOverwrite)
        {
            ConsistencyLevel[] allConsistencies = Enum.GetValues<ConsistencyLevel>();

            // All overrides when target is 'Strong' are VALID
            ConsistencyLevel desiredConsistencyLevel = ConsistencyLevel.Strong;
            foreach (ConsistencyLevel backendConsistencyLevel in allConsistencies)
            {
                if (backendConsistencyLevel == desiredConsistencyLevel)
                {
                    continue;
                }

                foreach (KeyValuePair<Documents.OperationType, bool> entry in ValidationHelpersTest.GetPerOperationExpectations())
                {
                    bool result = ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistencyLevel,
                        desiredConsistencyLevel,
                        isValidConsistencyLevelOverwrite,
                        (Documents.OperationType)entry.Key,
                        Documents.ResourceType.Document);

                    Assert.AreEqual(isValidConsistencyLevelOverwrite && entry.Value, result,
                        $"({isValidConsistencyLevelOverwrite}, {backendConsistencyLevel}, {desiredConsistencyLevel}) ({entry.Key}, {entry.Value})");
                }
            }
        }

        [TestMethod]
        [DataRow(ConsistencyLevel.Eventual, ConsistencyLevel.BoundedStaleness)]
        [DataRow(ConsistencyLevel.ConsistentPrefix, ConsistencyLevel.BoundedStaleness)]
        [DataRow(ConsistencyLevel.Session, ConsistencyLevel.BoundedStaleness)]
        public void TestIsValidConsistencyLevelOverwrite_BoundedPromotionsRejected(
            ConsistencyLevel backendConsistencyLevel,
            ConsistencyLevel desiredConsistencyLevel)
        {
            foreach (KeyValuePair<Documents.OperationType, bool> entry in ValidationHelpersTest.GetPerOperationExpectations())
            {
                bool result = ValidationHelpers.IsValidConsistencyLevelOverwrite(backendConsistencyLevel,
                    desiredConsistencyLevel,
                    true,
                    (Documents.OperationType)entry.Key,
                    Documents.ResourceType.Document);

                Assert.AreEqual(false, result,
                    $"({backendConsistencyLevel}, {desiredConsistencyLevel}) ({entry.Key}, {entry.Value})");
            }
        }

        private static Dictionary<Documents.OperationType, bool> GetPerOperationExpectations()
        {
            Dictionary<Documents.OperationType, bool> perOperationOverride = new Dictionary<Documents.OperationType, bool>();

            foreach (Documents.OperationType operationType in Enum.GetValues<Documents.OperationType>())
            {
                perOperationOverride.Add(
                        operationType,
                        Documents.OperationTypeExtensions.IsReadOperation(operationType)
                            && operationType != Documents.OperationType.Head
                            && operationType != Documents.OperationType.HeadFeed
                            && operationType != Documents.OperationType.QueryPlan
                            && operationType != Documents.OperationType.MetadataCheckAccess);
            }

            return perOperationOverride;
        }
    }
}