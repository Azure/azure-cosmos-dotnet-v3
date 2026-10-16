//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Regression bar for PR #5866 — verifies that the
    /// AZURE_COSMOS_USE_LENGTH_AWARE_RANGE_COMPARATOR environment variable
    /// controls the LengthAware range comparator end-to-end:
    ///
    ///   ENV var
    ///     -> ConfigurationManager.IsLengthAwareRangeComparatorEnabled()
    ///       -> CosmosClientOptions.UseLengthAwareRangeComparer (property initializer)
    ///         -> ClientContextCore (useLengthAwareRangeComparer ctor arg)
    ///           -> DocumentClient.UseLengthAwareRangeComparer (consumed by
    ///              PartitionKeyRangeCache.TryCombine).
    ///
    /// Before the fix, CosmosClientOptions.UseLengthAwareRangeComparer was a
    /// hardcoded "true" literal gated only by the INTERNAL compile-time symbol,
    /// so setting the env var to "false" had no effect on a real CosmosClient.
    /// </summary>
    [TestClass]
    public class LengthAwareRangeComparerEnvVarTests
    {
        private const string EnvVarName = "AZURE_COSMOS_USE_LENGTH_AWARE_RANGE_COMPARATOR";
        private const string AccountEndpoint = "https://localhost:8081/";

        // Default the runtime-build expects when the env var is unset.
        // Mirror the product code: true in all non-INTERNAL builds, false in INTERNAL.
#if INTERNAL
        private const bool DefaultWhenUnset = false;
#else
        private const bool DefaultWhenUnset = true;
#endif

        private string priorEnvVarValue;

        [TestInitialize]
        public void TestInitialize()
        {
            this.priorEnvVarValue = Environment.GetEnvironmentVariable(EnvVarName);
            Environment.SetEnvironmentVariable(EnvVarName, null);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Environment.SetEnvironmentVariable(EnvVarName, this.priorEnvVarValue);
        }

        [DataTestMethod]
        [DataRow(null, DefaultWhenUnset, DisplayName = "unset -> build default")]
        [DataRow("false", false, DisplayName = "false -> false")]
        [DataRow("true", true, DisplayName = "true -> true")]
        [Owner("amudumba")]
        public void IsLengthAwareRangeComparatorEnabled_RespectsEnvVar(string envValue, bool expected)
        {
            Environment.SetEnvironmentVariable(EnvVarName, envValue);

            Assert.AreEqual(
                expected,
                ConfigurationManager.IsLengthAwareRangeComparatorEnabled(),
                $"env={envValue ?? "<unset>"} did not produce expected={expected}.");
        }

        [DataTestMethod]
        [DataRow(null, DefaultWhenUnset, DisplayName = "unset -> build default")]
        [DataRow("false", false, DisplayName = "false -> false (regression for PR #5866)")]
        [DataRow("true", true, DisplayName = "true -> true")]
        [Owner("amudumba")]
        public void CosmosClientOptions_DefaultUseLengthAwareRangeComparer_RespectsEnvVar(string envValue, bool expected)
        {
            Environment.SetEnvironmentVariable(EnvVarName, envValue);

            CosmosClientOptions options = new CosmosClientOptions();

            Assert.AreEqual(
                expected,
                options.UseLengthAwareRangeComparer,
                $"env={envValue ?? "<unset>"} did not propagate to CosmosClientOptions.UseLengthAwareRangeComparer.");
        }

        [DataTestMethod]
        // env-var-only rows (no explicit option on CosmosClientOptions):
        [DataRow(null, null, DefaultWhenUnset, DisplayName = "unset, no explicit -> build default")]
        [DataRow("false", null, false, DisplayName = "false, no explicit -> false (regression for PR #5866)")]
        [DataRow("true", null, true, DisplayName = "true, no explicit -> true")]
        // explicit-option-wins row: env says off, caller explicitly opts back in:
        [DataRow("false", true, true, DisplayName = "false, explicit=true -> true (explicit option wins)")]
        [Owner("amudumba")]
        public void CosmosClient_UseLengthAwareRangeComparer_FlowsToDocumentClient(
            string envValue,
            bool? explicitOption,
            bool expected)
        {
            Environment.SetEnvironmentVariable(EnvVarName, envValue);

            CosmosClientOptions options = null;
            if (explicitOption.HasValue)
            {
                options = new CosmosClientOptions
                {
                    UseLengthAwareRangeComparer = explicitOption.Value,
                };
            }

            using CosmosClient cosmosClient = new CosmosClient(
                AccountEndpoint,
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                options);

            Assert.AreEqual(
                expected,
                cosmosClient.ClientOptions.UseLengthAwareRangeComparer,
                $"env={envValue ?? "<unset>"}, explicit={explicitOption?.ToString() ?? "<none>"} did not reach CosmosClient.ClientOptions on the real construction path.");

            Assert.AreEqual(
                expected,
                cosmosClient.DocumentClient.UseLengthAwareRangeComparer,
                $"env={envValue ?? "<unset>"}, explicit={explicitOption?.ToString() ?? "<none>"} did not flow through ClientContextCore into DocumentClient — this is the value PartitionKeyRangeCache.TryCombine consumes.");
        }
    }
}
