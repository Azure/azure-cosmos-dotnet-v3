//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [DoNotParallelize]
    public class ConfigurationManagerTests
    {
        [TestMethod]
        public void GetMaxOperationsInDirectModeBatchRequest_WhenEnvironmentValueChanges_Retries()
        {
            const string environmentVariableName = "AZURE_COSMOS_MAX_OPERATIONS_IN_BATCH_REQUEST";
            Environment.SetEnvironmentVariable(environmentVariableName, "invalid");
            ConfigurationManager.ResetMaxOperationsInDirectModeBatchRequestCacheForTesting();

            try
            {
                Assert.ThrowsException<ArgumentException>(
                    () => ConfigurationManager.GetMaxOperationsInDirectModeBatchRequest());

                Environment.SetEnvironmentVariable(environmentVariableName, "50");
                Assert.AreEqual(50, ConfigurationManager.GetMaxOperationsInDirectModeBatchRequest());
            }
            finally
            {
                Environment.SetEnvironmentVariable(environmentVariableName, null);
                ConfigurationManager.ResetMaxOperationsInDirectModeBatchRequestCacheForTesting();
            }
        }
    }
}