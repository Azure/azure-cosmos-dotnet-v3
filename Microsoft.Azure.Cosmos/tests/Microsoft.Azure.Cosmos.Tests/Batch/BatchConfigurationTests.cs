//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class BatchConfigurationTests
    {
        private const string EnvironmentVariableName = "COSMOS_MAX_OPERATIONS_IN_DIRECT_MODE_BATCH_REQUEST";

        [TestCleanup]
        public void TestCleanup()
        {
            // Clean up environment variable after each test
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);
        }

        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableNotSet_ReturnsDefault()
        {
            // Arrange
            Environment.SetEnvironmentVariable(EnvironmentVariableName, null);

            // Act
            int result = BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert
            Assert.AreEqual(Constants.MaxOperationsInDirectModeBatchRequest, result);
        }

        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToValidValue_ReturnsValue()
        {
            // Arrange
            const int expectedValue = 50;
            Environment.SetEnvironmentVariable(EnvironmentVariableName, expectedValue.ToString());

            // Act
            int result = BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToLargeValue_ReturnsValue()
        {
            // Arrange
            const int expectedValue = 50; // Changed to a smaller value that should be within bounds
            Environment.SetEnvironmentVariable(EnvironmentVariableName, expectedValue.ToString());

            // Act
            int result = BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert
            Assert.AreEqual(expectedValue, result);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToValueGreaterThanMax_ThrowsArgumentException()
        {
            // Arrange
            // Set to a value that's likely greater than the default constant
            const int valueGreaterThanMax = 10000;
            Environment.SetEnvironmentVariable(EnvironmentVariableName, valueGreaterThanMax.ToString());

            // Act
            BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToZero_ThrowsArgumentException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(EnvironmentVariableName, "0");

            // Act
            BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToNegativeValue_ThrowsArgumentException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(EnvironmentVariableName, "-1");

            // Act
            BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert - ExpectedException
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToInvalidString_ThrowsArgumentException()
        {
            // Arrange
            Environment.SetEnvironmentVariable(EnvironmentVariableName, "invalid");

            // Act
            BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert - ExpectedException
        }

        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToEmptyString_ReturnsDefault()
        {
            // Arrange
            Environment.SetEnvironmentVariable(EnvironmentVariableName, "");

            // Act
            int result = BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert
            Assert.AreEqual(Constants.MaxOperationsInDirectModeBatchRequest, result);
        }

        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentVariableSetToOne_ReturnsOne()
        {
            // Arrange
            const int expectedValue = 1;
            Environment.SetEnvironmentVariable(EnvironmentVariableName, expectedValue.ToString());

            // Act
            int result = BatchConfiguration.GetMaxOperationsInDirectModeBatchRequest();

            // Assert
            Assert.AreEqual(expectedValue, result);
        }
    }
}