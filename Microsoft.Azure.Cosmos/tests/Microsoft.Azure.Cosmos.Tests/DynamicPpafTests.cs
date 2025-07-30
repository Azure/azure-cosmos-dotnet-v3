//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DynamicPpafTests
    {
        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldUpdatePropertiesCorrectly()
        {
            // Arrange
            AccountProperties initialProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = false
            };

            AccountProperties updatedProperties = new AccountProperties()
            {
                Id = "testAccount", 
                EnablePartitionLevelFailover = true
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(initialProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act - Initialize with first properties
            await config.InitializeAsync();
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);

            // Update account properties
            config.UpdateAccountProperties(updatedProperties);

            // Assert
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover, "Account properties should be updated");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldHandleRepeatedUpdates()
        {
            // Arrange
            AccountProperties sameProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = true
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(sameProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover);

            // Update with same PPAF value
            AccountProperties duplicateProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = true // Same value
            };
            config.UpdateAccountProperties(duplicateProperties);

            // Assert - Properties should still be updated correctly
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover, "Account properties should remain updated");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldHandleNullPpafValues()
        {
            // Arrange
            AccountProperties initialProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = true
            };

            AccountProperties updatedProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = null  // Change to null
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(initialProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover);

            // Update with null PPAF value
            config.UpdateAccountProperties(updatedProperties);

            // Assert - Properties should be updated with null value
            Assert.IsNull(config.AccountProperties.EnablePartitionLevelFailover, "Account properties should have null PPAF value");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldHandleNullAccountProperties()
        {
            // Arrange
            AccountProperties initialProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = false
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(initialProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);

            // Update with null account properties - should not throw exception
            config.UpdateAccountProperties(null);

            // Assert - Original account properties should remain unchanged
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover, "Original account properties should remain unchanged");
        }
    }
}