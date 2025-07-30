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
        public async Task CosmosAccountServiceConfiguration_ShouldTriggerEventWhenPpafChanges()
        {
            // Arrange
            bool? receivedPpafValue = null;
            bool eventTriggered = false;

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
            config.OnEnablePartitionLevelFailoverChanged += (newValue) =>
            {
                receivedPpafValue = newValue;
                eventTriggered = true;
            };

            // Act - Initialize with first properties
            await config.InitializeAsync();
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);
            Assert.IsFalse(eventTriggered); // Should not trigger on initial load

            // Simulate GlobalEndpointManager updating the account properties
            config.UpdateAccountProperties(updatedProperties);

            // Assert
            Assert.IsTrue(eventTriggered, "Event should have been triggered when PPAF value changed");
            Assert.AreEqual(true, receivedPpafValue, "Event should have received the new PPAF value");
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover, "Account properties should be updated");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldNotTriggerEventWhenPpafDoesNotChange()
        {
            // Arrange
            bool eventTriggered = false;

            AccountProperties sameProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = true
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(sameProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);
            config.OnEnablePartitionLevelFailoverChanged += (newValue) =>
            {
                eventTriggered = true;
            };

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

            // Assert
            Assert.IsFalse(eventTriggered, "Event should not be triggered when PPAF value doesn't change");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldHandleNullPpafValues()
        {
            // Arrange
            bool? receivedPpafValue = null;
            bool eventTriggered = false;

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
            config.OnEnablePartitionLevelFailoverChanged += (newValue) =>
            {
                receivedPpafValue = newValue;
                eventTriggered = true;
            };

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover);

            // Update with null PPAF value
            config.UpdateAccountProperties(updatedProperties);

            // Assert - Event should trigger when changing from true to null
            Assert.IsTrue(eventTriggered, "Event should be triggered when PPAF value changes from true to null");
            Assert.IsNull(receivedPpafValue, "Event should receive null PPAF value");
            Assert.IsNull(config.AccountProperties.EnablePartitionLevelFailover, "Account properties should have null PPAF value");
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task CosmosAccountServiceConfiguration_ShouldHandleNullAccountProperties()
        {
            // Arrange
            bool eventTriggered = false;

            AccountProperties initialProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = false
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(initialProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);
            config.OnEnablePartitionLevelFailoverChanged += (newValue) =>
            {
                eventTriggered = true;
            };

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);

            // Update with null account properties - should not trigger event or throw exception
            config.UpdateAccountProperties(null);

            // Assert
            Assert.IsFalse(eventTriggered, "Event should not be triggered when account properties are null");
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover, "Original account properties should remain unchanged");
        }
    }
}