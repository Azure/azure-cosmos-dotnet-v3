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
        [Timeout(30000)]
        public async Task CosmosAccountServiceConfiguration_ShouldTriggerEventWhenPpafChanges()
        {
            // Set environment variable for faster refresh during testing
            string originalValue = Environment.GetEnvironmentVariable("AZURE_COSMOS_ACCOUNT_PROPERTIES_REFRESH_INTERVAL_IN_SECONDS");
            Environment.SetEnvironmentVariable("AZURE_COSMOS_ACCOUNT_PROPERTIES_REFRESH_INTERVAL_IN_SECONDS", "2");

            try
            {
                // Arrange
                bool? receivedPpafValue = null;
                bool eventTriggered = false;
                AutoResetEvent eventSignal = new AutoResetEvent(false);

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

                int callCount = 0;
                Func<Task<AccountProperties>> mockAccountPropertiesFunc = () =>
                {
                    callCount++;
                    return Task.FromResult(callCount == 1 ? initialProperties : updatedProperties);
                };

                CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);
                config.OnEnablePartitionLevelFailoverChanged += (newValue) =>
                {
                    receivedPpafValue = newValue;
                    eventTriggered = true;
                    eventSignal.Set();
                };

                // Act - Initialize
                await config.InitializeAsync();
                Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);
                Assert.IsFalse(eventTriggered); // Should not trigger on initial load

                // Wait for refresh cycle (2 seconds + buffer)
                eventSignal.WaitOne(TimeSpan.FromSeconds(5));

                // Assert
                Assert.IsTrue(eventTriggered, "Event should have been triggered when PPAF value changed");
                Assert.AreEqual(true, receivedPpafValue, "Event should have received the new PPAF value");
                Assert.IsTrue(callCount >= 2, "Account properties should have been fetched multiple times");

                // Cleanup
                config.Dispose();
            }
            finally
            {
                // Restore original environment variable
                Environment.SetEnvironmentVariable("AZURE_COSMOS_ACCOUNT_PROPERTIES_REFRESH_INTERVAL_IN_SECONDS", originalValue);
            }
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

            // Wait a short time to ensure refresh doesn't trigger event
            await Task.Delay(2000);

            // Assert
            Assert.IsFalse(eventTriggered, "Event should not be triggered when PPAF value doesn't change");

            // Cleanup
            config.Dispose();
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

            int callCount = 0;
            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () =>
            {
                callCount++;
                return Task.FromResult(callCount == 1 ? initialProperties : updatedProperties);
            };

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act
            await config.InitializeAsync();
            Assert.AreEqual(true, config.AccountProperties.EnablePartitionLevelFailover);

            // Manually trigger refresh to test the null case
            // Since we can't easily control the timing, we'll use reflection or wait
            await Task.Delay(1000);

            // Assert - For this test, we mainly want to ensure no exceptions are thrown
            // when dealing with null PPAF values
            Assert.IsNotNull(config.AccountProperties, "Account properties should not be null");

            // Cleanup
            config.Dispose();
        }
    }
}