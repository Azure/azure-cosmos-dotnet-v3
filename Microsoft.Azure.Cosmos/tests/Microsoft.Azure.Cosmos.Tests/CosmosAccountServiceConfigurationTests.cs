//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for <see cref="CosmosAccountServiceConfiguration"/> initialization.
    ///
    /// Regression coverage for issue #4671: when the gateway account read yields no account
    /// properties (a null <see cref="AccountProperties"/>), client initialization
    /// (<c>DocumentClient.InitializeGatewayConfigurationReaderAsync</c>) dereferenced
    /// <c>accountProperties.EnableMultipleWriteLocations</c> and threw a bare, undiagnosable
    /// <see cref="NullReferenceException"/> on the first request at startup. The fix validates the
    /// account properties at the point they enter the SDK (here) and throws a descriptive,
    /// actionable exception instead, while preserving the existing self-heal behavior (a subsequent
    /// initialization attempt with a valid account read succeeds).
    /// </summary>
    [TestClass]
    public class CosmosAccountServiceConfigurationTests
    {
        [TestMethod]
        public async Task InitializeAsync_WhenAccountReadReturnsProperties_SetsAccountProperties()
        {
            AccountProperties expected = new AccountProperties();
            CosmosAccountServiceConfiguration configuration = new CosmosAccountServiceConfiguration(
                () => Task.FromResult(expected));

            await configuration.InitializeAsync();

            Assert.AreSame(expected, configuration.AccountProperties);
        }

        [TestMethod]
        public async Task InitializeAsync_WhenAccountReadReturnsNull_ThrowsDescriptiveException()
        {
            CosmosAccountServiceConfiguration configuration = new CosmosAccountServiceConfiguration(
                () => Task.FromResult<AccountProperties>(null));

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => configuration.InitializeAsync());

            // The message must be actionable: it must name the null condition and point at the most
            // common root cause (a Microsoft.Azure.Cosmos / Microsoft.Azure.Cosmos.Direct version mismatch).
            Assert.IsTrue(
                exception.Message.Contains("AccountProperties"),
                $"Exception message should name the null AccountProperties condition. Actual: {exception.Message}");
            Assert.IsTrue(
                exception.Message.Contains("Microsoft.Azure.Cosmos.Direct"),
                $"Exception message should hint at the package-version-mismatch root cause. Actual: {exception.Message}");
        }

        /// <summary>
        /// Issue #4671 reproduction + fix pin. Before the fix, <see cref="CosmosAccountServiceConfiguration.InitializeAsync"/>
        /// completed normally with a null <see cref="CosmosAccountServiceConfiguration.AccountProperties"/>, so the very
        /// next member access threw a bare <see cref="NullReferenceException"/> — exactly what
        /// <c>DocumentClient.InitializeGatewayConfigurationReaderAsync</c> did when reading
        /// <c>accountProperties.EnableMultipleWriteLocations</c>. After the fix, a descriptive
        /// <see cref="InvalidOperationException"/> is raised from <c>InitializeAsync</c> itself.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_WhenAccountReadReturnsNull_DoesNotSurfaceOpaqueNullReferenceException()
        {
            CosmosAccountServiceConfiguration configuration = new CosmosAccountServiceConfiguration(
                () => Task.FromResult<AccountProperties>(null));

            try
            {
                await configuration.InitializeAsync();

                // Reproduction of the reported crash: with a null AccountProperties, the next member
                // access dereferences null (mirrors DocumentClient reading accountProperties.EnableMultipleWriteLocations).
                _ = configuration.QueryEngineConfiguration;

                Assert.Fail("Expected a descriptive InvalidOperationException from InitializeAsync when the account read returns null.");
            }
            catch (InvalidOperationException)
            {
                // Fixed behavior: a descriptive, actionable failure is raised from InitializeAsync.
            }
            catch (NullReferenceException)
            {
                Assert.Fail("Regression (issue #4671): a null account-properties read surfaced as an opaque NullReferenceException instead of a descriptive error.");
            }
        }

        /// <summary>
        /// Self-heal contract: <c>DocumentClient</c> creates a fresh
        /// <see cref="CosmosAccountServiceConfiguration"/> on every initialization attempt, so a
        /// subsequent attempt whose account read succeeds must initialize cleanly after a prior
        /// null-read failure. This matches the reported "first request fails, all subsequent requests
        /// succeed" behavior.
        /// </summary>
        [TestMethod]
        public async Task InitializeAsync_AfterNullReadFailure_FreshConfigurationSucceeds()
        {
            CosmosAccountServiceConfiguration failing = new CosmosAccountServiceConfiguration(
                () => Task.FromResult<AccountProperties>(null));
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => failing.InitializeAsync());

            AccountProperties recovered = new AccountProperties();
            CosmosAccountServiceConfiguration succeeding = new CosmosAccountServiceConfiguration(
                () => Task.FromResult(recovered));
            await succeeding.InitializeAsync();

            Assert.AreSame(recovered, succeeding.AccountProperties);
        }
    }
}
