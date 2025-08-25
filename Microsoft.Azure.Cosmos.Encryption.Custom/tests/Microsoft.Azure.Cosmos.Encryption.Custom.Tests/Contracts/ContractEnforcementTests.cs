namespace Microsoft.Azure.Cosmos.Encryption.Tests.Contracts
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.Azure.Cosmos.Encryption.Custom;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
    // Run contract enforcement only on net6.0 to keep a single, stable baseline.
#if NET8_0_OR_GREATER
    [TestMethod]
    [Ignore]
    public void ContractChanges()
    {
        // Intentionally skipped on .NET 8+: contract baselines are maintained for net6.0 only.
    }
#else
    [TestMethod]
    public void ContractChanges()
    {
            // Anchor to force-load the assembly through a referenced public type (no explicit Assembly.Load).
            _ = typeof(CosmosEncryptor);

            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContractContainBreakingChanges(
                assembly: typeof(CosmosEncryptor).Assembly,
                baselinePath: "DotNetSDKEncryptionCustomAPI.json",
                breakingChangesPath: "DotNetSDKEncryptionCustomAPIChanges.json");
    }
#endif
    }
}
