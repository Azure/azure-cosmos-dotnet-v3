//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Tests.Authorization
{
    using System;
    using global::Azure.Core;
    using Microsoft.Azure.Cosmos.Authorization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosScopeProviderTests
    {
        private static readonly Uri TestAccountEndpoint = new Uri("https://testaccount.documents.azure.com:443/");

        [DataTestMethod]
        [DataRow("https://override/.default", "https://override/.default", DisplayName = "OverrideScope_Used")]
        [DataRow(null, "https://testaccount.documents.azure.com/.default", DisplayName = "AccountScope_Used_WhenNoOverride")]
        public void GetTokenRequestContext_UsesExpectedScope(string overrideScope, string expectedScope)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", overrideScope);

            try
            {
                CosmosScopeProvider provider = new CosmosScopeProvider(TestAccountEndpoint);
                TokenRequestContext context = provider.GetTokenRequestContext();
                Assert.AreEqual(expectedScope, context.Scopes[0]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);
            }
        }

        [DataTestMethod]
        [DataRow("https://override/.default", false, "AADSTS500011", "https://override/.default", DisplayName = "OverrideScope_NeverFallback")]
        [DataRow(null, true, "AADSTS500011", "https://cosmos.azure.com/.default", DisplayName = "AccountScope_FallbacksToAadDefault")]
        [DataRow(null, false, "SomeOtherError", "https://testaccount.documents.azure.com/.default", DisplayName = "AccountScope_NoFallbackOnOtherError")]
        public void Test_TryFallback_Behavior(
            string overrideScope,
            bool expectFallback,
            string exceptionMessage,
            string expectedScope)
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", overrideScope);

            try
            {
                CosmosScopeProvider provider = new CosmosScopeProvider(TestAccountEndpoint);

                bool didFallback = provider.TryFallback(new Exception(exceptionMessage));

                Assert.AreEqual(expectFallback, didFallback, "Fallback result mismatch.");
                Assert.AreEqual(expectedScope, provider.GetTokenRequestContext().Scopes[0]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);
            }
        }

        [TestMethod]
        public void TryFallback_DoesNotFallback_WhenAlreadyUsingAadDefault()
        {
            Environment.SetEnvironmentVariable("AZURE_COSMOS_AAD_SCOPE_OVERRIDE", null);
            CosmosScopeProvider provider = new CosmosScopeProvider(TestAccountEndpoint);

            provider.TryFallback(new Exception("AADSTS500011"));
            Assert.AreEqual("https://cosmos.azure.com/.default", provider.GetTokenRequestContext().Scopes[0]);

            // Act
            bool didFallbackAgain = provider.TryFallback(new Exception("AADSTS500011"));

            Assert.IsFalse(didFallbackAgain, "Should not fallback again when already using AadDefault scope.");
            Assert.AreEqual("https://cosmos.azure.com/.default", provider.GetTokenRequestContext().Scopes[0]);
        }
    }
}