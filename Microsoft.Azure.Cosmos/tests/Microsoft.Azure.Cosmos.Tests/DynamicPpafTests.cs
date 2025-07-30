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
        public async Task CosmosAccountServiceConfiguration_ShouldInitializeCorrectly()
        {
            // Arrange
            AccountProperties initialProperties = new AccountProperties()
            {
                Id = "testAccount",
                EnablePartitionLevelFailover = false
            };

            Func<Task<AccountProperties>> mockAccountPropertiesFunc = () => Task.FromResult(initialProperties);

            CosmosAccountServiceConfiguration config = new CosmosAccountServiceConfiguration(mockAccountPropertiesFunc);

            // Act - Initialize with properties
            await config.InitializeAsync();

            // Assert
            Assert.AreEqual(false, config.AccountProperties.EnablePartitionLevelFailover);
            Assert.AreEqual("testAccount", config.AccountProperties.Id);
        }
    }
}