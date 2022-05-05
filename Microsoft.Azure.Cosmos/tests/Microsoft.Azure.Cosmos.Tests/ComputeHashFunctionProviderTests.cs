//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Security;
    using global::Azure;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos.Tests.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="ComputeHashFunctionProvider"/>.
    /// </summary>
    [TestClass]
    public sealed class ComputeHashFunctionProviderTests
    {
        private const string AccountEndpoint = "https://test-account.documents.local.com";
        private static readonly string AccountKeyString = TestKeyGenerator.GenerateAuthKey();
        private static readonly SecureString AccountKeySecureString = SecureStringUtility.ConvertToSecureString(AccountKeyString);

        [TestMethod]
        public void ComputeHashFunctionProviderFromStringKey()
        {
            using ComputeHashFunctionProvider credential = ComputeHashFunctionProvider.From(ComputeHashFunctionProviderTests.AccountKeyString);

            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();
        }

        [TestMethod]
        public void ComputeHashFunctionProviderFromSecureString()
        {
            using ComputeHashFunctionProvider credential = ComputeHashFunctionProvider.From(ComputeHashFunctionProviderTests.AccountKeySecureString);

            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<SecureStringHMACSHA256Helper>();
        }

        [TestMethod]
        public void ComputeHashFunctionProviderFromAzureCredentialWithUpdate()
        {
            AzureKeyCredential azureKeyCredential = new AzureKeyCredential(ComputeHashFunctionProviderTests.AccountKeyString);
            using ComputeHashFunctionProvider credential = ComputeHashFunctionProvider.From(azureKeyCredential);

            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();
            IComputeHash previousHash = credential.HashFunction;

            // Update key
            azureKeyCredential.Update(TestKeyGenerator.GenerateAuthKey());
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();
            credential.HashFunction.Should().NotBe(previousHash);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Empty String")]
        [DataRow(default(string), DisplayName = "Null String")]
        public void InvalidStringKeyNegativeTest(string keyValue)
        {
            FluentActions.Invoking(() => ComputeHashFunctionProvider.From(keyValue)).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void NullSecureStringKeyNegativeTest()
        {
            FluentActions.Invoking(() => ComputeHashFunctionProvider.From(default(SecureString))).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void NullCredentialKeyNegativeTest()
        {
            FluentActions.Invoking(() => ComputeHashFunctionProvider.From(default(AzureKeyCredential))).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void MalformedKeyNegativeTest()
        {
            FluentActions.Invoking(() => ComputeHashFunctionProvider.From("Not Base64 Bytes")).Should().Throw<FormatException>();
            FluentActions.Invoking(() => ComputeHashFunctionProvider.From(new AzureKeyCredential("Not Base64 Bytes"))).Should().Throw<FormatException>();
        }
    }
}
