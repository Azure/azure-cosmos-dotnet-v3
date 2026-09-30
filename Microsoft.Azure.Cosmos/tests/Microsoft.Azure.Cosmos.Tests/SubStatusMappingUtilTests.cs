//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Microsoft.Azure.Cosmos.Util;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Test class for <see cref="SubStatusMappingUtil"/>
    /// </summary>
    [TestClass]
    public class SubStatusMappingUtilTests
    {
        [TestMethod]
        [DataRow(412, 2018, "CanNotAcquireMasterPartitionAccessLock")]
        [DataRow(412, 2028, "PartitionMigrationIsDisabledOnTheGlobalDatabaseAccount")]
        [DataRow(412, 2029, "PartitionMigrationIsDisabledOnTheRunnerAccount")]
        [DataRow(412, 2100, "AccountAlreadyInTargetGateway")]
        [DataRow(500, 3004, "FederationDoesNotExistOrIsLocked")]
        [DataRow(429, 3088, "ThrottleDueToSplit")]
        [DataRow(409, 3302, "PartitionKeyHashCollisionForId")]
        [DataRow(401, 6053, "FabricTokenValidationFailed")]
        [DataRow(401, 6056, "InvalidFabricArtifactId")]
        public void GetSubStatusCodeString_MisspelledMember_ReturnsCorrectedName(int statusCode, int subStatusCode, string expected)
        {
            // Act
            string result = SubStatusMappingUtil.GetSubStatusCodeString((StatusCodes)statusCode, (SubStatusCodes)subStatusCode);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void GetSubStatusCodeString_CorrectlySpelledMember_ReturnsMemberName()
        {
            // Act
            string result = SubStatusMappingUtil.GetSubStatusCodeString(StatusCodes.TooManyRequests, SubStatusCodes.RUBudgetExceeded);

            // Assert
            Assert.AreEqual(nameof(SubStatusCodes.RUBudgetExceeded), result);
        }

        [TestMethod]
        public void GetSubStatusCodeString_SharedValue_KeepsStatusBasedMapping()
        {
            // Act
            string notFound = SubStatusMappingUtil.GetSubStatusCodeString(StatusCodes.NotFound, (SubStatusCodes)1002);
            string gone = SubStatusMappingUtil.GetSubStatusCodeString(StatusCodes.Gone, (SubStatusCodes)1002);

            // Assert
            Assert.AreEqual(nameof(SubStatusCodes.ReadSessionNotAvailable), notFound);
            Assert.AreEqual(nameof(SubStatusCodes.PartitionKeyRangeGone), gone);
        }
    }
}
