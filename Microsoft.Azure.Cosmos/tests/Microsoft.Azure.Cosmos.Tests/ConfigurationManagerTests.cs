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


    }
}