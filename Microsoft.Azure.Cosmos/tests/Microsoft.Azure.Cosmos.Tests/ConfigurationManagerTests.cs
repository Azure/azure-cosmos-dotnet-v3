//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ConfigurationManagerTests
    {
        [TestMethod]
        public void GetCircuitBreakerTimeoutCounterResetWindowInMinutes_DefaultValue()
        {
            // Test that the default value is returned when environment variable is not set
            int result = ConfigurationManager.GetCircuitBreakerTimeoutCounterResetWindowInMinutes(5);
            Assert.AreEqual(5, result);
        }

        [TestMethod]
        public void GetCircuitBreakerTimeoutCounterResetWindowInMinutes_CustomDefaultValue()
        {
            // Test that custom default values are respected
            int result = ConfigurationManager.GetCircuitBreakerTimeoutCounterResetWindowInMinutes(10);
            Assert.AreEqual(10, result);
        }

        [TestMethod]
        public void GetCircuitBreakerTimeoutCounterResetWindowInMinutes_EnvironmentVariableOverride()
        {
            // Test that environment variable overrides the default value
            const string envVarName = "AZURE_COSMOS_PPCB_TIMEOUT_COUNTER_RESET_WINDOW_IN_MINUTES";
            const string testValue = "15";
            
            try
            {
                Environment.SetEnvironmentVariable(envVarName, testValue);
                int result = ConfigurationManager.GetCircuitBreakerTimeoutCounterResetWindowInMinutes(5);
                Assert.AreEqual(15, result);
            }
            finally
            {
                // Clean up environment variable
                Environment.SetEnvironmentVariable(envVarName, null);
            }
        }

        [TestMethod]
        public void PartitionLevelFailoverEnvironmentVariableRemoved()
        {
            // Verify that the deprecated environment variable constant is no longer available
            // This test ensures the AZURE_COSMOS_PARTITION_LEVEL_FAILOVER_ENABLED constant was removed
            Type configManagerType = typeof(ConfigurationManager);
            var field = configManagerType.GetField("PartitionLevelFailoverEnabled", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            
            Assert.IsNull(field, "PartitionLevelFailoverEnabled constant should have been removed");
        }

        [TestMethod]
        public void IsPartitionLevelFailoverEnabledMethodRemoved()
        {
            // Verify that the deprecated IsPartitionLevelFailoverEnabled method is no longer available
            Type configManagerType = typeof(ConfigurationManager);
            var method = configManagerType.GetMethod("IsPartitionLevelFailoverEnabled", 
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            
            Assert.IsNull(method, "IsPartitionLevelFailoverEnabled method should have been removed");
        }
    }
}