namespace Microsoft.Azure.Cosmos.Encryption
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;    
    using Moq;
    using global::Azure;
    using global::Azure.Core;
    using global::Azure.Security.KeyVault.Keys;
    using global::Azure.Security.KeyVault.Keys.Cryptography;
    using System.IO;
    using System.Security.Cryptography;
    using System.Collections.Generic;
    using Castle.Core.Internal;

    internal class KeyVaultAccessClientTests
    {
        /// <summary>
        ///  Test Class for Mocking KeyClient methods.
        /// </summary>
        internal class TestKeyClient : KeyClient
        {
            Uri vaultUri { get; }
            TokenCredential credential { get; }

            Dictionary<string, string> keyinfo = new Dictionary<string, string>
            {
                {"testkey1","Recoverable"},
                {"testkey2","nothingset"}               
            };

            /// <summary>
            /// Initializes a new instance of the TestKeyClient class for the specified vaultUri.
            /// </summary>
            /// <param name="vaultUri"> Key Vault Uri </param>
            /// <param name="credential"> Token Credentials </param>
            internal TestKeyClient(Uri vaultUri, TokenCredential credential)
            {
                if( vaultUri == null || credential == null)
                {
                    throw new ArgumentNullException("Value is null.");
                }

                this.vaultUri = vaultUri;
                this.credential = credential;
            }

            /// <summary>
            /// Simulates a GetKeyAsync method of KeyVault SDK.
            /// </summary>
            /// <param name="name"></param>
            /// <param name="version"></param>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public override Task<Response<KeyVaultKey>> GetKeyAsync(string name, string version = null, CancellationToken cancellationToken = default)
            {
                Console.WriteLine("Accessing Key via Test GetKeyAsync");

                // simulate a RequestFailed Exception
                if(name.Contains(KeyVaultTestConstants.ValidateRequestFailedEx))
                {
                    throw new RequestFailedException("Service Unavailable");
                }

                // simulate a case to return a Null Key.
                if (name.Contains(KeyVaultTestConstants.ValidateNullKeyVaultKey))
                {   
                    Mock<Response<KeyVaultKey>> mockedResponseNullKeyVault = new Mock<Response<KeyVaultKey>>();
                    mockedResponseNullKeyVault.SetupGet(r => r.Value).Returns((KeyVaultKey)null);
                    return Task.FromResult(mockedResponseNullKeyVault.Object);
                }

                this.keyinfo.TryGetValue(name, out string recoverlevel);
                KeyProperties tp = KeyModelFactory.KeyProperties(recoveryLevel:recoverlevel);
                JsonWebKey jwk = KeyModelFactory.JsonWebKey(KeyType.Ec, curveName: "invalid", keyOps: new[] { KeyOperation.Sign, KeyOperation.Verify });
                KeyVaultKey mockKey = KeyModelFactory.KeyVaultKey(properties:tp,key: jwk);

                Mock<Response<KeyVaultKey>> mockedResponseKeyVault = new Mock<Response<KeyVaultKey>>();
                mockedResponseKeyVault.SetupGet(r => r.Value).Returns(mockKey);

                return Task.FromResult(mockedResponseKeyVault.Object);
            }
        }

        /// <summary>
        /// Factory Class for KeyClientFactory.
        /// Returns an instance of TestKeyClient for mocking KeyClient.
        /// </summary>
        internal class KeyClientTestFactory : KeyClientFactory
        {
            public override KeyClient GetKeyClient(KeyVaultKeyUriProperties keyVaultKeyUriProperties, TokenCredential tokenCred)
            {                
                return new TestKeyClient(keyVaultKeyUriProperties.KeyUri, tokenCred);               
            }
        }

        /// <summary>
        /// Test Class for mocking CryptographyClient methods.
        /// </summary>
        internal class TestCryptographyClient : CryptographyClient
        {
            Uri keyId { get; }
            TokenCredential credential { get; }

            byte[] secretkey = new byte[16] { 0x12, 0x10, 0x20, 0x40, 060, 0x23, 0x12, 0x19, 0x22, 0x10, 0x09, 0x12, 0x99, 0x12, 0x11, 0x22 };
            byte[] iv = new byte[16] { 0x99, 0x99, 0x88, 0x88, 0x77, 0x77, 0x66, 0x66, 0x55, 0x55, 0x44, 0x44, 0x33, 0x33, 0x22, 0x22 };

            /// <summary>
            /// Initializes a new instance of the TestCryptographyClient class for the specified keyid.
            /// </summary>
            /// <param name="keyid"></param>
            /// <param name="credential"></param>
            internal TestCryptographyClient(Uri keyid, TokenCredential credential)
            {
                if( keyid == null || credential == null)
                {
                    throw new ArgumentNullException("Value is null.");
                }
                this.keyId = keyid;
                this.credential = credential;
            }

            /// <summary>
            /// Simulates WrapKeyAsync method of KeyVault SDK.
            /// </summary>
            /// <param name="algorithm"> Encryption Algorithm </param>
            /// <param name="key"> Key to be wrapped </param>
            /// <param name="cancellationToken"> cancellation token </param>
            /// <returns></returns>
            public override Task<WrapResult> WrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] key, CancellationToken cancellationToken = default)
            {

                if(key.IsNullOrEmpty())
                {
                    throw new ArgumentNullException("Key is Null.");
                }

                byte[] wrappedKey = this.Encrypt(key, this.secretkey, this.iv);

                // simulate a null wrapped key
                if (this.keyId.ToString().Contains(KeyVaultTestConstants.ValidateNullWrappedKey))
                {
                    wrappedKey = null;
                }

                string keyid = "12345678910";
                WrapResult mockWrapResult = CryptographyModelFactory.WrapResult(keyId: keyid, key:wrappedKey, algorithm: KeyVaultConstants.RsaOaep256);
                
                return Task.FromResult(mockWrapResult);
            }

            /// <summary>
            /// Simulates UnwrapKeyAsync of KeyVault SDK.
            /// </summary>
            /// <param name="algorithm"> Encryption Algorithm </param>
            /// <param name="encryptedKey"> Key to be unwrapped </param>
            /// <param name="cancellationToken"> cancellation token </param>
            /// <returns></returns>
            public override Task<UnwrapResult> UnwrapKeyAsync(KeyWrapAlgorithm algorithm, byte[] encryptedKey, CancellationToken cancellationToken = default)
            {
                if (encryptedKey.IsNullOrEmpty())
                {
                    throw new ArgumentNullException("Key is Null.");
                }

                byte[] unwrappedKey = this.Decrypt(encryptedKey, this.secretkey, this.iv);

                // simulate a null unwrapped key.
                if (this.keyId.ToString().Contains(KeyVaultTestConstants.ValidateNullUnwrappedKey))
                {
                    unwrappedKey = null;
                }

                string keyid = "12345678910";
                UnwrapResult mockUnwrapResult = CryptographyModelFactory.UnwrapResult(keyId: keyid, key:unwrappedKey, algorithm: KeyVaultConstants.RsaOaep256);
                return Task.FromResult(mockUnwrapResult);
            }

            private byte[] PerformCryptography(byte[] data, ICryptoTransform cryptoTransform)
            {
                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cryptoStream = new CryptoStream(ms, cryptoTransform, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                    cryptoStream.FlushFinalBlock();

                    return ms.ToArray();
                }
            }

            private byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 128;
                    aes.BlockSize = 128;
                    aes.Padding = PaddingMode.Zeros;

                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                    {
                        return this.PerformCryptography(data, encryptor);
                    }
                }
            }

            private byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
            {
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 128;
                    aes.BlockSize = 128;
                    aes.Padding = PaddingMode.Zeros;

                    aes.Key = key;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                    {
                        return this.PerformCryptography(data, decryptor);
                    }
                }
            }
        }

        /// <summary>
        /// Factory Class for CryptographyClient.
        /// Returns an instance of TestCryptographyClient for mocking CryptographyClient.
        /// </summary>
        internal class CryptographyClientFactoryTestFactory : CryptographyClientFactory
        {
            public override CryptographyClient GetCryptographyClient(KeyVaultKeyUriProperties keyVaultKeyUriProperties, TokenCredential tokenCred)
            {
                return new TestCryptographyClient(keyVaultKeyUriProperties.KeyUri, tokenCred);
            }
        }

        internal class KeyVaultTestConstants
        {
            internal const string ValidateNullWrappedKey = "nullWrappedKeyByte";
            internal const string ValidateNullUnwrappedKey = "nullUnwrappedKeyByte";
            internal const string ValidateRequestFailedEx = "requestFailed";
            internal const string ValidateNullKeyVaultKey = "nullKeyVaultKey";
        }
    }
}
