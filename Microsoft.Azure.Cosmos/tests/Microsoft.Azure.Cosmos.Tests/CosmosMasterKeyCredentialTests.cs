//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Security;
    using FluentAssertions;
    using Microsoft.Azure.Cosmos.Tests.Utils;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests for <see cref="CosmosMasterKeyCredential"/>.
    /// </summary>
    [TestClass]
    public sealed class CosmosMasterKeyCredentialTests
    {
        private const string AccountEndpoint = "https://test-account.documents.local.com";
        private static readonly string AccountKeyString = TestKeyGenerator.GenerateAuthKey();
        private static readonly SecureString AccountKeySecureString = SecureStringUtility.ConvertToSecureString(AccountKeyString);

        [TestMethod]
        public void CreateAndUpdateStringKeyCredentialTest()
        {
            using CosmosMasterKeyCredential credential = new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, CosmosMasterKeyCredentialTests.AccountKeyString);

            credential.HashFunctionCount.Should().Be(1);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();

            // Update the key and the hash function should have changed.
            credential.UpdateKey(TestKeyGenerator.GenerateAuthKey());
            credential.HashFunctionCount.Should().Be(2);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();

            // Update with a secure stirng key and it should have changed again.
            credential.UpdateKey(SecureStringUtility.ConvertToSecureString(TestKeyGenerator.GenerateAuthKey()));
            credential.HashFunctionCount.Should().Be(3);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<SecureStringHMACSHA256Helper>();
        }

        [TestMethod]
        public void CreateAndUpdateSecureStringKeyCredentialTest()
        {
            using CosmosMasterKeyCredential credential = new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, CosmosMasterKeyCredentialTests.AccountKeySecureString);

            credential.HashFunctionCount.Should().Be(1);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<SecureStringHMACSHA256Helper>();

            // Update the key and the hash function should have changed.
            credential.UpdateKey(SecureStringUtility.ConvertToSecureString(TestKeyGenerator.GenerateAuthKey()));
            credential.HashFunctionCount.Should().Be(2);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<SecureStringHMACSHA256Helper>();

            // Update with a secure stirng key and it should have changed again.
            credential.UpdateKey(TestKeyGenerator.GenerateAuthKey());
            credential.HashFunctionCount.Should().Be(3);
            credential.HashFunction.Should().NotBeNull();
            credential.HashFunction.Should().BeOfType<StringHMACSHA256Hash>();
        }

        [TestMethod]
        public void AccountEndpointIsConstantTest()
        {
            using CosmosMasterKeyCredential credential = new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, CosmosMasterKeyCredentialTests.AccountKeyString);

            credential.Endpoint.ToString().Should().Be(CosmosMasterKeyCredentialTests.AccountEndpoint);

            // Update the key doesn't affect the account endpoint
            credential.UpdateKey(TestKeyGenerator.GenerateAuthKey());
            credential.Endpoint.ToString().Should().Be(CosmosMasterKeyCredentialTests.AccountEndpoint);
        }

        [DataTestMethod]
        [DataRow("", DisplayName = "Empty String")]
        [DataRow(default(string), DisplayName = "Null String")]
        public void InvalidStringKeyNegativeTest(string keyValue)
        {
            FluentActions.Invoking(() => new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, keyValue)).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void NullSecureStringKeyNegativeTest()
        {
            SecureString ssKey = default;
            FluentActions.Invoking(() => new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, ssKey)).Should().Throw<ArgumentNullException>();
        }

        [TestMethod]
        public void MalformedKeyNegativeTest()
        {
            FluentActions.Invoking(() => new CosmosMasterKeyCredential(CosmosMasterKeyCredentialTests.AccountEndpoint, "Not Base64 Bytes")).Should().Throw<FormatException>();
        }
    }
}
