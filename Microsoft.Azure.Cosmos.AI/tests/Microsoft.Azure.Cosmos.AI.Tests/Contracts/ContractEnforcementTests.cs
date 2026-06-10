namespace Microsoft.Azure.Cosmos.AI.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestCategory("Windows")]
    [TestCategory("UpdateContract")]
    [TestClass]
    public class ContractEnforcementTests
    {
        /// <summary>
        /// Locks the public API surface of <c>Microsoft.Azure.Cosmos.AI</c> against a
        /// versioned baseline so accidental renames, visibility changes, or removals
        /// surface at PR review time instead of shipping to a preview NuGet.
        ///
        /// IMPORTANT: Because tests can run on multiple .NET versions, the contract
        /// validation uses framework-specific baselines to ensure consistency:
        ///
        /// - When running on net8.0: validates against DotNetSDKCosmosAIAPI.net8.json
        ///
        /// To update baselines, run: UpdateContracts.ps1 from the repository root.
        /// </summary>
        [TestMethod]
        public void ContractChanges()
        {
            Cosmos.Tests.Contracts.ContractEnforcement.ValidateContract(
                dllName: "Microsoft.Azure.Cosmos.AI",
                contractType: Cosmos.Tests.Contracts.ContractType.Standard,
                baselinePattern: "DotNetSDKCosmosAIAPI",
                breakingChangesPattern: "DotNetSDKCosmosAIAPIChanges");
        }
    }
}
