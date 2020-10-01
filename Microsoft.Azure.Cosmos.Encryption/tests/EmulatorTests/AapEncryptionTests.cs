//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.Data.AAP_PH.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using static Microsoft.Azure.Cosmos.Encryption.KeyVaultAccessClientTests;
    using DataEncryptionKey = Microsoft.Azure.Cosmos.Encryption.DataEncryptionKey;

    [TestClass]
    public class AapEncryptionTests
    {
        private static readonly Uri masterKeyUri1 = new Uri("https://demo.keyvault.net/keys/samplekey1/03ded886623sss09bzc60351e536a111");
        private static readonly Uri masterKeyUri2 = new Uri("https://demo.keyvault.net/keys/samplekey2/47d306aeaaeyyyaabs9467235460dc22");
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata(name: "metadata1", value: masterKeyUri1.ToString());
        private static readonly EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata(name: "metadata2", value: masterKeyUri2.ToString());
        private const string dekId = "mydek";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container encryptionContainer;
        private static Container keyContainer;
        private static TestEncryptionKeyStoreProvider testKeyStoreProvider;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;
        private static string decryptionFailedDocId;

        private static byte[] rawDekForKeyVault;
        private static Uri keyVaultKeyUri;
        private static AzureKeyVaultKeyWrapMetadata azureKeyVaultKeyWrapMetadata;
        private static AzureKeyVaultKeyWrapProvider azureKeyVaultKeyWrapProvider;
        private static EncryptionTestsTokenCredentialFactory encryptionTestsTokenCredentialFactory;

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            AapEncryptionTests.testKeyStoreProvider = new TestEncryptionKeyStoreProvider();
            AapEncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(AapEncryptionTests.testKeyStoreProvider);
            AapEncryptionTests.encryptor = new TestEncryptor(AapEncryptionTests.dekProvider);

            AapEncryptionTests.client = TestCommon.CreateCosmosClient();
            AapEncryptionTests.database = await AapEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            AapEncryptionTests.keyContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await AapEncryptionTests.dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);

            AapEncryptionTests.itemContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            AapEncryptionTests.encryptionContainer = AapEncryptionTests.itemContainer.WithAapEncryptor(encryptor);
            AapEncryptionTests.dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, AapEncryptionTests.dekId);

            AapEncryptionTests.rawDekForKeyVault = DataEncryptionKey.Generate(CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized);
            AapEncryptionTests.encryptionTestsTokenCredentialFactory = new EncryptionTestsTokenCredentialFactory();
            AapEncryptionTests.azureKeyVaultKeyWrapProvider = new AzureKeyVaultKeyWrapProvider(encryptionTestsTokenCredentialFactory, new KeyClientTestFactory(), new CryptographyClientFactoryTestFactory());
            keyVaultKeyUri = new Uri("https://testdemo1.vault.azure.net/keys/testkey1/47d306aeaae74baab294672354603ca3");
            AapEncryptionTests.azureKeyVaultKeyWrapMetadata = new AzureKeyVaultKeyWrapMetadata(keyVaultKeyUri);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (AapEncryptionTests.database != null)
            {
                using (await AapEncryptionTests.database.DeleteStreamAsync()) { }
            }

            if (AapEncryptionTests.client != null)
            {
                AapEncryptionTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(name: "metadata1", value: AapEncryptionTests.metadata1.Value),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionCreateDekWithNonAapAlgorithmFails()
        {
            string dekId = "oldDek";
            try
            {
                await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId, CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
                Assert.Fail();
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("For use of 'AEAes256CbcHmacSha256Randomized' algorithm, DekProvider needs to be initialized with EncryptionKeyWrapProvider.", ex.Message);
            }
        }

        /// <summary>
        /// Validates UnWrapKey via KeyVault Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task UnWrapKeyUsingKeyVault()
        {
            CancellationToken cancellationToken = default;
            EncryptionKeyWrapResult wrappedKey = await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);
            byte[] wrappedDek = wrappedKey.WrappedDataEncryptionKey;
            EncryptionKeyWrapMetadata wrappedKeyVaultMetaData = wrappedKey.EncryptionKeyWrapMetadata;
            
            EncryptionKeyUnwrapResult keyUnwrapResponse = await AapEncryptionTests.UnwrapDekKeyVaultAsync(wrappedDek, wrappedKeyVaultMetaData, cancellationToken);

            Assert.IsNotNull(keyUnwrapResponse);
            Assert.IsNotNull(keyUnwrapResponse.ClientCacheTimeToLive);
            Assert.IsNotNull(keyUnwrapResponse.DataEncryptionKey);

            CollectionAssert.AreEqual(rawDekForKeyVault, keyUnwrapResponse.DataEncryptionKey);
        }

        /// <summary>
        /// Validates handling of PurgeProtection Settings.
        /// </summary>
        /// <returns></returns>
        [TestMethod]        
        public async Task SetKeyVaultValidatePurgeProtectionAndSoftDeleteSettingsAsync()
        {
            CancellationToken cancellationToken = default;

            KeyVaultAccessClient keyVaultAccessClient = new KeyVaultAccessClient(encryptionTestsTokenCredentialFactory, new KeyClientTestFactory(), new CryptographyClientFactoryTestFactory());

            keyVaultKeyUri = new Uri("https://testdemo3.vault.azure.net/keys/testkey1/47d306aeaae74baab294672354603ca3");
            AzureKeyVaultKeyWrapMetadata wrapKeyVaultMetaData = new AzureKeyVaultKeyWrapMetadata(keyVaultKeyUri);

            KeyVaultKeyUriProperties.TryParse(new Uri(wrapKeyVaultMetaData.Value), out KeyVaultKeyUriProperties keyVaultUriProperties);
            bool validatepurgeprotection = await keyVaultAccessClient.ValidatePurgeProtectionAndSoftDeleteSettingsAsync(keyVaultUriProperties, cancellationToken);

            Assert.AreEqual(validatepurgeprotection, true);
        }

        /// <summary>
        /// Validates handling of PurgeProtection Settings.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task NotSetKeyVaultValidatePurgeProtectionAndSoftDeleteSettingsAsync()
        {
            CancellationToken cancellationToken = default;

            KeyVaultAccessClient keyVaultAccessClient = new KeyVaultAccessClient(encryptionTestsTokenCredentialFactory, new KeyClientTestFactory(), new CryptographyClientFactoryTestFactory());

            Uri keyVaultKeyUriPurgeTest = new Uri("https://testdemo2.vault.azure.net/keys/testkey2/ad47829797dc46489223cc5da3cba3ca");
            AzureKeyVaultKeyWrapMetadata wrapKeyVaultMetaData = new AzureKeyVaultKeyWrapMetadata(keyVaultKeyUriPurgeTest);
            KeyVaultKeyUriProperties.TryParse(new Uri(wrapKeyVaultMetaData.Value), out KeyVaultKeyUriProperties keyVaultUriProperties);

            bool validatepurgeprotection = await keyVaultAccessClient.ValidatePurgeProtectionAndSoftDeleteSettingsAsync(keyVaultUriProperties, cancellationToken);

            Assert.AreEqual(validatepurgeprotection, false);
        }

        /// <summary>
        /// Validates handling of Null Wrapped Key Returned from Key Vault
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException),
        "ArgumentNullException when provided with null key.")]
        public async Task ValidateNullWrappedKeyResult()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/keys/testkey1/" + KeyVaultTestConstants.ValidateNullWrappedKey);
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "akv", value: keyUri.AbsoluteUri);           

            await azureKeyVaultKeyWrapProvider.WrapKeyAsync(
                rawDekForKeyVault,
                invalidWrapMetadata,
                cancellationToken);         
        }

        /// <summary>
        /// Validates handling of Null Unwrapped Key from Key Vault
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException),
        "ArgumentNullException when provided with null key.")]
        public async Task ValidateNullUnwrappedKeyResult()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/keys/testkey1/" + KeyVaultTestConstants.ValidateNullUnwrappedKey);
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata("akv", keyUri.AbsoluteUri, algorithm: KeyVaultConstants.RsaOaep256);

            EncryptionKeyWrapResult wrappedKey = await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);
            byte[] wrappedDek = wrappedKey.WrappedDataEncryptionKey;            

            await azureKeyVaultKeyWrapProvider.UnwrapKeyAsync(
               wrappedDek,
               invalidWrapMetadata,
               cancellationToken);            
        }

        /// <summary>
        /// Validates Null Response from KeyVault
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException),
        "ArgumentException Caught if KeyVault Responds with a Null Key")]
        public async Task ValidateKeyClientReturnsNullKeyVaultResponse()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/keys/" + KeyVaultTestConstants.ValidateNullKeyVaultKey + "/47d306aeaae74baab294672354603ca3");
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "akv", value: keyUri.AbsoluteUri);
            await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Validates handling of Wrapping of Dek.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task WrapKeyUsingKeyVault()
        {
            CancellationToken cancellationToken = default;
            EncryptionKeyWrapResult keyWrapResponse = await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);

            Assert.IsNotNull(keyWrapResponse);
            Assert.IsNotNull(keyWrapResponse.EncryptionKeyWrapMetadata);
            Assert.IsNotNull(keyWrapResponse.WrappedDataEncryptionKey);
        }

        /// <summary>
        /// Validates handling of KeyClient returning a Request Failed.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(KeyVaultAccessException),
        "ArgumentNullException Method catches and returns RequestFailed Exception KeyVaultAccess Client to throw inner exception")]
        public async Task ValidateKeyClientReturnRequestFailed()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/keys/" + KeyVaultTestConstants.ValidateRequestFailedEx + "/47d306aeaae74baab294672354603ca3");
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "akv", value: keyUri.AbsoluteUri);
            await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Integration validation of DEK Provider with KeyVault Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task EncryptionCreateDekKeyVaultWrapProvider()
        {
            string dekId = "DekWithKeyVault";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);

            Assert.AreEqual(
                metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(azureKeyVaultKeyWrapProvider);
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        /// <summary>
        /// Validates handling of Null Key Passed to Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException),
        "ArgumentNullException when provided with null key.")]
        public async Task WrapNullKeyUsingKeyVault()
        {
            CancellationToken cancellationToken = default;
            await AapEncryptionTests.WrapDekKeyVaultAsync(null, azureKeyVaultKeyWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Integration testing for Rewrapping Dek with KeyVault Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task EncryptionRewrapDekWithKeyVaultWrapProvider()
        {
            string dekId = "randomDekKeyVault";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await AapEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                AapEncryptionTests.metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = AapEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata2,
                dekProperties.EncryptionKeyWrapMetadata);

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(azureKeyVaultKeyWrapProvider);
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        /// <summary>
        /// Validates handling of Incorrect Wrap Meta to Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException),
        "ArgumentException when provided with incorrect WrapMetaData TypeConstants")]
        public async Task WrapKeyUsingKeyVaultInValidTypeConstants()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/keys/testkey1/47d306aeaae74baab294672354603ca3");
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "incorrectConstant", value: keyUri.AbsoluteUri);
            await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Simulates a KeyClient Constructor returning an ArgumentNullException.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException),
        "ArgumentNullException Method catches and returns NullException")]
        public async Task ValidateKeyClientReturnNullArgument()
        {
            CancellationToken cancellationToken = default;            
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "akv", value: null);
            await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Validate Azure Key Wrap Provider handling of Incorrect KeyVault Uris.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException),
        "ArgumentException when provided with incorrect WrapMetaData Value")]
        public async Task WrapKeyUsingKeyVaultInValidValue()
        {
            CancellationToken cancellationToken = default;
            Uri keyUri = new Uri("https://testdemo.vault.azure.net/key/testkey1/47d306aeaae74baab294672354603ca3");
            EncryptionKeyWrapMetadata invalidWrapMetadata = new EncryptionKeyWrapMetadata(type: "akv", value: keyUri.AbsoluteUri);
            await AapEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        [TestMethod]
        public async Task EncryptionRewrapDek()
        {
            string dekId = "randomDek";
            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await AapEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                AapEncryptionTests.metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = AapEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata2,
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionRewrapDekEtagMismatch()
        {
            string dekId = "dummyDek";
            EncryptionKeyWrapMetadata newMetadata = new EncryptionKeyWrapMetadata(name: "newMetadata", value: "newMetadataValue");

            DataEncryptionKeyProperties dekProperties = await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                AapEncryptionTests.metadata1,
                dekProperties.EncryptionKeyWrapMetadata);

            // modify dekProperties directly, which would lead to etag change
            DataEncryptionKeyProperties updatedDekProperties = new DataEncryptionKeyProperties(
                dekProperties.Id,
                dekProperties.EncryptionAlgorithm,
                dekProperties.WrappedDataEncryptionKey,
                dekProperties.EncryptionKeyWrapMetadata,
                DateTime.UtcNow);
            await AapEncryptionTests.keyContainer.ReplaceItemAsync<DataEncryptionKeyProperties>(
                updatedDekProperties,
                dekProperties.Id,
                new PartitionKey(dekProperties.Id));

            // rewrap should succeed, despite difference in cached value
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await AapEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                newMetadata);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = AapEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                newMetadata,
                dekProperties.EncryptionKeyWrapMetadata);

            Assert.AreEqual(2, AapEncryptionTests.testKeyStoreProvider.WrapKeyCallsCount[newMetadata.Value]);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            await dekProvider.InitializeAsync(AapEncryptionTests.database, AapEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await AapEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
                await dekProvider.InitializeAsync(AapEncryptionTests.database, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await AapEncryptionTests.CreateDekAsync(dekProvider, contosoV1);
                await AapEncryptionTests.CreateDekAsync(dekProvider, contosoV2);
                await AapEncryptionTests.CreateDekAsync(dekProvider, fabrikamV1);
                await AapEncryptionTests.CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    "SELECT * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id ASC");

                // Test pagination
                await AapEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c",
                    itemCountInPage: 3);
            }
            finally
            {
                await newKeyContainer.DeleteContainerStreamAsync();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await AapEncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await AapEncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions());
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithoutPartitionKey()
        {
            TestDoc testDoc = TestDoc.Create();
            try
            {
                await AapEncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    requestOptions: AapEncryptionTests.GetRequestOptions(AapEncryptionTests.dekId, TestDoc.PathsToEncrypt));
                Assert.Fail("CreateItem should've failed because PartitionKey was not provided.");
            }
            catch (NotSupportedException ex)
            {
                Assert.AreEqual("partitionKey cannot be null for operations using EncryptionContainer.", ex.Message);
            }
        }

        [TestMethod]
        public async Task EncryptionFailsWithUnknownDek()
        {
            string unknownDek = "unknownDek";

            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, unknownDek, TestDoc.PathsToEncrypt);
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual($"Failed to retrieve Data Encryption Key with id: '{unknownDek}'.", ex.Message);
                Assert.IsTrue(ex.InnerException is CosmosException);
            }
        }

        [TestMethod]
        public async Task EncryptionCreateItem()
        {
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, testDoc);

            await AapEncryptionTests.VerifyItemByReadStreamAsync(AapEncryptionTests.encryptionContainer, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            // Read feed (null query)
            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            await AapEncryptionTests.ValidateSprocResultsAsync(
                AapEncryptionTests.encryptionContainer,
                expectedDoc);

            expectedDoc.Sensitive = null;

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            
            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2);

            // change feed processor
            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // induce failure
            AapEncryptionTests.encryptor.FailDecryption = true;
            AapEncryptionTests.decryptionFailedDocId = testDoc1.Id;

            await AapEncryptionTests.VerifyItemByReadAsync(
                AapEncryptionTests.encryptionContainer,
                testDoc1,
                AapEncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler(),
                compareEncryptedProperty: false);

            await AapEncryptionTests.VerifyItemByReadStreamAsync(
                AapEncryptionTests.encryptionContainer,
                testDoc1,
                AapEncryptionTests.GetItemRequestOptionsWithDecryptionResultHandler(),
                compareEncryptedProperty: false);

            EncryptionQueryRequestOptions queryRequestOptions = new EncryptionQueryRequestOptions
            {
                DecryptionResultHandler = AapEncryptionTests.ErrorHandler
            };

            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                AapEncryptionTests.encryptionContainer,
                testDoc1,
                testDoc2,
                query,
                queryRequestOptions,
                false);

            // GetItemLinqQueryable
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(
                AapEncryptionTests.encryptionContainer, 
                testDoc1, 
                testDoc2, 
                query: null,
                queryRequestOptions,
                false);

            //await this.ValidateChangeFeedIteratorResponse(
            //    AapEncryptionTests.encryptionContainer,
            //    testDoc1,
            //    testDoc2,
            //    AapEncryptionTests.ErrorHandler);

            // await this.ValidateChangeFeedProcessorResponse(EncryptionTests.itemContainerCore, testDoc1, testDoc2, false);
            AapEncryptionTests.encryptor.FailDecryption = false;
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query += " ORDER BY c._ts";
            await AapEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(AapEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(
                AapEncryptionTests.encryptionContainer,
                AapEncryptionTests.dekId,
                new List<string>() { "/Sensitive", "/NonSensitive" });

            TestDoc expectedDoc = new TestDoc(testDoc);

            await AapEncryptionTests.ValidateQueryResultsAsync(
                AapEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable with ToEncryptionStreamIterator extension
            await AapEncryptionTests.ValidateQueryResponseAsync(AapEncryptionTests.encryptionContainer);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await AapEncryptionTests.UpsertItemAsync(
                AapEncryptionTests.encryptionContainer,
                TestDoc.Create(),
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await AapEncryptionTests.UpsertItemAsync(
                AapEncryptionTests.encryptionContainer,
                testDoc,
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await AapEncryptionTests.ReplaceItemAsync(
                AapEncryptionTests.encryptionContainer,
                updatedDoc,
                AapEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, replacedDoc);

            await AapEncryptionTests.DeleteItemAsync(AapEncryptionTests.encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = AapEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await AapEncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, AapEncryptionTests.itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);

            int failureCount = 0;
            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(AapEncryptionTests.database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(AapEncryptionTests.itemContainer.Id);
            Action<DecryptionResult> errorHandler = (decryptionErrorDetails) =>
            {
                Assert.AreEqual(decryptionErrorDetails.Exception.Message, $"The CosmosDataEncryptionKeyProvider was not initialized.");
                failureCount++;
            };
            Container encryptionContainerForRestrictedUser = containerForRestrictedUser.WithAapEncryptor(encryptor);

            await AapEncryptionTests.PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, AapEncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await AapEncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(AapEncryptionTests.dekId), "DEK.ReadAsync");

            await encryptionContainerForRestrictedUser.ReadItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions
                {
                    DecryptionResultHandler = errorHandler
                });
            Assert.AreEqual(failureCount, 1);

            await encryptionContainerForRestrictedUser.ReadItemStreamAsync(
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                new EncryptionItemRequestOptions
                {
                    DecryptionResultHandler = errorHandler
                });
            Assert.AreEqual(failureCount, 2);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = AapEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await AapEncryptionTests.database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, AapEncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestEncryptionKeyStoreProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(keyManagerUserPermission.Token);

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(AapEncryptionTests.database.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, AapEncryptionTests.keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(AapEncryptionTests.dekId);
            Assert.AreEqual(AapEncryptionTests.dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (ArgumentException ex)
            {
                Assert.AreEqual("PathsToEncrypt includes a invalid path: '/id'.", ex.Message);
            }

            try
            {
                await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            Database databaseWithBulk = clientWithBulk.GetDatabase(AapEncryptionTests.database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(AapEncryptionTests.itemContainer.Id);
            Container encryptionContainerWithBulk = containerWithBulk.WithAapEncryptor(AapEncryptionTests.encryptor);

            List<Task> tasks = new List<Task>()
            {
                AapEncryptionTests.CreateItemAsync(encryptionContainerWithBulk, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                AapEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                AapEncryptionTests.ReplaceItemAsync(encryptionContainerWithBulk, docToReplace, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                AapEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, docToUpsert, AapEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                AapEncryptionTests.DeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = AapEncryptionTests.dekId;
            string dek2 = "dek2Forbatch";
            await AapEncryptionTests.CreateDekAsync(AapEncryptionTests.dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc4ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await AapEncryptionTests.CreateItemAsync(AapEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await AapEncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, AapEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .CreateItem(doc4ToCreate, AapEncryptionTests.GetBatchItemRequestOptions(dek1, new List<string>())) // empty PathsToEncrypt list
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, AapEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), AapEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc3ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc4ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToReplace);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToReplace);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc1ToUpsert);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.encryptionContainer, doc2ToUpsert);

            ResponseMessage readResponseMessage = await AapEncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToCreate, compareEncryptedProperty: false);

            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToCreate, compareEncryptedProperty: false);

            // doc3ToCreate, doc4ToCreate wasn't encrypted
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc3ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc4ToCreate);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToReplace, compareEncryptedProperty: false);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToReplace, compareEncryptedProperty: false);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc1ToUpsert, compareEncryptedProperty: false);
            await AapEncryptionTests.VerifyItemByReadAsync(AapEncryptionTests.itemContainer, doc2ToUpsert, compareEncryptedProperty: false);
        }

        private static async Task ValidateSprocResultsAsync(Container container, TestDoc expectedDoc)
        {
            string sprocId = Guid.NewGuid().ToString();
            string sprocBody = @"function(docId) {
                var context = getContext();
                var collection = context.getCollection();
                var docUri =  collection.getAltLink() + '/docs/' + docId;
                var response = context.getResponse();

                collection.readDocument(docUri, { },
                    function(error, resource, options) {
                        response.setBody(resource);
                    });
            }";

            StoredProcedureResponse storedProcedureResponse =
                await container.Scripts.CreateStoredProcedureAsync(new StoredProcedureProperties(sprocId, sprocBody));
            Assert.AreEqual(HttpStatusCode.Created, storedProcedureResponse.StatusCode);

            StoredProcedureExecuteResponse<TestDoc> sprocResponse = await container.Scripts.ExecuteStoredProcedureAsync<TestDoc>(
                sprocId,
                new PartitionKey(expectedDoc.PK),
                parameters: new dynamic[] { expectedDoc.Id });

            expectedDoc.EqualsExceptEncryptedProperty(sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            Container container,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null
                ? new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                }
                : null;

            FeedIterator<TestDoc> queryResponseIterator = query != null
                ? container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions)
                : container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                Assert.AreEqual(expectedDoc, readDoc);
            }
            else
            {
                Assert.AreEqual(0, readDocs.Count);
            }
        }

        private static async Task ValidateQueryResultsMultipleDocumentsAsync(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            string query,
            QueryRequestOptions requestOptions = null,
            bool compareEncryptedProperty = true)
        {
            FeedIterator<TestDoc> queryResponseIterator;

            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                queryResponseIterator = container.ToEncryptionFeedIterator<TestDoc>(linqQueryable, requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            if (query == null)
            {
                Assert.IsTrue(readDocs.Count >= 2);
            }
            else
            {
                Assert.AreEqual(2, readDocs.Count);
            }

            for (int index = 0; index < readDocs.Count; index++)
            {
                if (readDocs.ElementAt(index).Id.Equals(testDoc1.Id))
                {
                    if (compareEncryptedProperty)
                    {
                        Assert.AreEqual(readDocs.ElementAt(index), testDoc1);
                    }
                    else
                    {
                        testDoc1.EqualsExceptEncryptedProperty(readDocs.ElementAt(index));
                    }
                }
                else if (readDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                {
                    if (compareEncryptedProperty)
                    {
                        Assert.AreEqual(readDocs.ElementAt(index), testDoc2);
                    }
                    else
                    {
                        testDoc2.EqualsExceptEncryptedProperty(readDocs.ElementAt(index));
                    }
                }
            }
        }

        private static async Task ValidateQueryResponseAsync(Container container,
            string query = null)
        {
            FeedIterator feedIterator;
            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                feedIterator = container.ToEncryptionStreamIterator(linqQueryable);
            }
            else
            {
                feedIterator = container.GetItemQueryStreamIterator(query);
            }

            while (feedIterator.HasMoreResults)
            {
                ResponseMessage response = await feedIterator.ReadNextAsync();
                Assert.IsTrue(response.IsSuccessStatusCode);
                Assert.IsNull(response.ErrorMessage);
            }
        }

        private async Task ValidateChangeFeedIteratorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2,
            Action<DecryptionResult> DecryptionResultHandler = null)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                continuationToken: null,
                new EncryptionChangeFeedRequestOptions()
                {
                    StartTime = DateTime.MinValue.ToUniversalTime(),
                    DecryptionResultHandler = DecryptionResultHandler
                });

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            while (changeIterator.HasMoreResults)
            {
                FeedResponse<TestDoc> testDocs = await changeIterator.ReadNextAsync();
                for (int index = 0; index < testDocs.Count; index++)
                {
                    if (testDocs.Resource.ElementAt(index).Id.Equals(testDoc1.Id) || testDocs.Resource.ElementAt(index).Id.Equals(testDoc2.Id))
                    {
                        changeFeedReturnedDocs.Add(testDocs.Resource.ElementAt(index));
                    }
                }
            }

            Assert.AreEqual(changeFeedReturnedDocs.Count, 2);
            Assert.AreEqual(testDoc1, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 2]);
            Assert.AreEqual(testDoc2, changeFeedReturnedDocs[changeFeedReturnedDocs.Count - 1]);
        }
        
        private async Task ValidateChangeFeedProcessorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            ChangeFeedProcessor cfp = container.GetChangeFeedProcessorBuilder(
                "testCFP",
                (IReadOnlyCollection<TestDoc> changes, CancellationToken cancellationToken)
                =>
                {
                    changeFeedReturnedDocs.AddRange(changes);
                    return Task.CompletedTask;
                })
                //.WithInMemoryLeaseContainer()
                //.WithStartFromBeginning()
                .Build();

            await cfp.StartAsync();
            await Task.Delay(2000);
            await cfp.StopAsync();

            Assert.IsTrue(changeFeedReturnedDocs.Count >= 2);

            foreach (TestDoc testDoc in changeFeedReturnedDocs)
            {
                if (testDoc.Id.Equals(testDoc1.Id))
                {
                    Assert.AreEqual(testDoc1, testDoc);
                }
                else if (testDoc.Id.Equals(testDoc2.Id))
                {
                    Assert.AreEqual(testDoc2, testDoc);
                }
            }
        }

        private static void ErrorHandler(DecryptionResult decryptionErrorDetails)
        {
            Assert.AreEqual(decryptionErrorDetails.Exception.Message, "Null DataEncryptionKey returned.");

            using (MemoryStream memoryStream = new MemoryStream(decryptionErrorDetails.EncryptedStream.ToArray()))
            {
                JObject itemJObj = TestCommon.FromStream<JObject>(memoryStream);
                JProperty encryptionPropertiesJProp = itemJObj.Property("_ei");
                Assert.IsNotNull(encryptionPropertiesJProp);
                Assert.AreEqual(itemJObj.Property("id").Value.ToString(), AapEncryptionTests.decryptionFailedDocId);
            }                
        }

        private static ItemRequestOptions GetItemRequestOptionsWithDecryptionResultHandler()
        {
            return new EncryptionItemRequestOptions
            {
                DecryptionResultHandler = AapEncryptionTests.ErrorHandler
            };
        }

        private static CosmosClient GetClient()
        {
            return TestCommon.CreateCosmosClient();
        }

        private static async Task IterateDekFeedAsync(
            CosmosDataEncryptionKeyProvider dekProvider,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string query,
            int? itemCountInPage = null)
        {
            int remainingItemCount = expectedDekIds.Count;
            QueryRequestOptions requestOptions = null;
            if (itemCountInPage.HasValue)
            {
                requestOptions = new QueryRequestOptions()
                {
                    MaxItemCount = itemCountInPage
                };
            }

            FeedIterator<DataEncryptionKeyProperties> dekIterator = dekProvider.DataEncryptionKeyContainer
                .GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    query,
                    requestOptions: requestOptions);

            Assert.IsTrue(dekIterator.HasMoreResults);

            List<string> readDekIds = new List<string>();
            while (remainingItemCount > 0)
            {
                FeedResponse<DataEncryptionKeyProperties> page = await dekIterator.ReadNextAsync();
                if (itemCountInPage.HasValue)
                {
                    // last page
                    if (remainingItemCount < itemCountInPage.Value)
                    {
                        Assert.AreEqual(remainingItemCount, page.Count);
                    }
                    else
                    {
                        Assert.AreEqual(itemCountInPage.Value, page.Count);
                    }
                }
                else
                {
                    Assert.AreEqual(expectedDekIds.Count, page.Count);
                }

                remainingItemCount -= page.Count;
                if (isExpectedDeksCompleteSetForRequest)
                {
                    Assert.AreEqual(remainingItemCount > 0, dekIterator.HasMoreResults);
                }

                foreach (DataEncryptionKeyProperties dek in page.Resource)
                {
                    readDekIds.Add(dek.Id);
                }
            }

            if (isResultOrderExpected)
            {
                Assert.IsTrue(expectedDekIds.SequenceEqual(readDekIds));
            }
            else
            {
                Assert.IsTrue(expectedDekIds.ToHashSet().SetEquals(readDekIds));
            }
        }

        private static async Task<ItemResponse<TestDoc>> UpsertItemAsync(
            Container container,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            HttpStatusCode expectedStatusCode)
        {
            ItemResponse<TestDoc> upsertResponse = await container.UpsertItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
            Assert.AreEqual(expectedStatusCode, upsertResponse.StatusCode);
            Assert.AreEqual(testDoc, upsertResponse.Resource);
            return upsertResponse;
        }

        private static async Task<ItemResponse<TestDoc>> CreateItemAsync(
            Container container,
            string dekId,
            List<string> pathsToEncrypt,
            string partitionKey = null)
        {
            TestDoc testDoc = TestDoc.Create(partitionKey);
            ItemResponse<TestDoc> createResponse = await container.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK),
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
            return createResponse;
        }

        private static async Task<ItemResponse<TestDoc>> ReplaceItemAsync(
            Container encryptedContainer,
            TestDoc testDoc,
            string dekId,
            List<string> pathsToEncrypt,
            string etag = null)
        {
            ItemResponse<TestDoc> replaceResponse = await encryptedContainer.ReplaceItemAsync(
                testDoc,
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                AapEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt, etag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponse.StatusCode);
            Assert.AreEqual(testDoc, replaceResponse.Resource);
            return replaceResponse;
        }

        private static async Task<ItemResponse<TestDoc>> DeleteItemAsync(
            Container encryptedContainer,
            TestDoc testDoc)
        {
            ItemResponse<TestDoc> deleteResponse = await encryptedContainer.DeleteItemAsync<TestDoc>(
                testDoc.Id,
                new PartitionKey(testDoc.PK));

            Assert.AreEqual(HttpStatusCode.NoContent, deleteResponse.StatusCode);
            Assert.IsNull(deleteResponse.Resource);
            return deleteResponse;
        }

        private static EncryptionItemRequestOptions GetRequestOptions(
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new EncryptionItemRequestOptions
            {
                EncryptionOptions = AapEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static TransactionalBatchItemRequestOptions GetBatchItemRequestOptions(
            string dekId,
            List<string> pathsToEncrypt,
            string ifMatchEtag = null)
        {
            return new EncryptionTransactionalBatchItemRequestOptions
            {
                EncryptionOptions = AapEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
                IfMatchEtag = ifMatchEtag
            };
        }

        private static EncryptionOptions GetEncryptionOptions(
            string dekId,
            List<string> pathsToEncrypt)
        {
            return new EncryptionOptions()
            {
                DataEncryptionKeyId = dekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = pathsToEncrypt
            };
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null, bool compareEncryptedProperty = true)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            if (compareEncryptedProperty)
            {
                Assert.AreEqual(testDoc, readDoc);
            }
            else
            {
                testDoc.EqualsExceptEncryptedProperty(readDoc);
            }
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null, bool compareEncryptedProperty = true)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            if (compareEncryptedProperty)
            {
                Assert.AreEqual(testDoc, readResponse.Resource);
            }
            else
            {
                testDoc.EqualsExceptEncryptedProperty(readResponse.Resource);
            }
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId, string algorithm = null)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                algorithm ?? CosmosEncryptionAlgorithm.AapAEAes256CbcHmacSha256Randomized,
                AapEncryptionTests.metadata1);

            Assert.AreEqual(HttpStatusCode.Created, dekResponse.StatusCode);
            
            return VerifyDekResponse(dekResponse,
                dekId);
        }

        private static async Task<EncryptionKeyWrapResult> WrapDekKeyVaultAsync(byte[] rawDek, EncryptionKeyWrapMetadata wrapMetaData, CancellationToken cancellationToken)
        {            
            EncryptionKeyWrapResult keyWrapResponse = await azureKeyVaultKeyWrapProvider.WrapKeyAsync(                
                rawDek,
                wrapMetaData,
                cancellationToken);

            return keyWrapResponse;
        }

        private static async Task<EncryptionKeyUnwrapResult> UnwrapDekKeyVaultAsync(byte[] wrappedDek, EncryptionKeyWrapMetadata unwrapMetaData, CancellationToken cancellationToken)
        {
            EncryptionKeyUnwrapResult keyUnwrapResponse = await azureKeyVaultKeyWrapProvider.UnwrapKeyAsync(
                wrappedDek,
                unwrapMetaData,
                cancellationToken);

            return keyUnwrapResponse;
        }

        private static DataEncryptionKeyProperties VerifyDekResponse(
            ItemResponse<DataEncryptionKeyProperties> dekResponse,
            string dekId)
        {
            Assert.IsTrue(dekResponse.RequestCharge > 0);
            Assert.IsNotNull(dekResponse.ETag);

            DataEncryptionKeyProperties dekProperties = dekResponse.Resource;
            Assert.IsNotNull(dekProperties);
            Assert.AreEqual(dekResponse.ETag, dekProperties.ETag);
            Assert.AreEqual(dekId, dekProperties.Id);
            Assert.IsNotNull(dekProperties.SelfLink);
            Assert.IsNotNull(dekProperties.CreatedTime);
            Assert.IsNotNull(dekProperties.LastModified);
            
            return dekProperties;
        }

        private static async Task PerformForbiddenOperationAsync(Func<Task> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected resource token based client to not be able to perform {operationName}");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
            {
            }
        }

        private static async Task PerformOperationOnUninitializedDekProviderAsync(Func<Task> func, string operationName)
        {
            try
            {
                await func();
                Assert.Fail($"Expected {operationName} to not work on uninitialized CosmosDataEncryptionKeyProvider.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.IsTrue(ex.Message.Contains("The CosmosDataEncryptionKeyProvider was not initialized."));
            }
        }

        public class TestDoc
        {
            public static List<string> PathsToEncrypt { get; } = new List<string>() { "/Sensitive" };

            [JsonProperty("id")]
            public string Id { get; set; }

            public string PK { get; set; }

            public string NonSensitive { get; set; }

            public string Sensitive { get; set; }

            public TestDoc()
            {
            }

            public TestDoc(TestDoc other)
            {
                this.Id = other.Id;
                this.PK = other.PK;
                this.NonSensitive = other.NonSensitive;
                this.Sensitive = other.Sensitive;
            }

            public override bool Equals(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive == doc.Sensitive;
            }

            public bool EqualsExceptEncryptedProperty(object obj)
            {
                return obj is TestDoc doc
                       && this.Id == doc.Id
                       && this.PK == doc.PK
                       && this.NonSensitive == doc.NonSensitive
                       && this.Sensitive != doc.Sensitive;
            }

            public override int GetHashCode()
            {
                int hashCode = 1652434776;
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Id);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.PK);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.NonSensitive);
                hashCode = (hashCode * -1521134295) + EqualityComparer<string>.Default.GetHashCode(this.Sensitive);
                return hashCode;
            }

            public static TestDoc Create(string partitionKey = null)
            {
                return new TestDoc()
                {
                    Id = Guid.NewGuid().ToString(),
                    PK = partitionKey ?? Guid.NewGuid().ToString(),
                    NonSensitive = Guid.NewGuid().ToString(),
                    Sensitive = Guid.NewGuid().ToString()
                };
            }

            public Stream ToStream()
            {
                return TestCommon.ToStream(this);
            }
        }

        private class TestEncryptionKeyStoreProvider : EncryptionKeyStoreProvider
        {
            readonly Dictionary<string, int> keyinfo = new Dictionary<string, int>
            {
                {masterKeyUri1.ToString(), 1},
                {masterKeyUri2.ToString(), 2},
            };

            public Dictionary<string, int> WrapKeyCallsCount { get; set; }

            public TestEncryptionKeyStoreProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();
            }

            public override string ProviderName => "TESTKEYSTORE_VAULT";

            public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedKey)
            {
                this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                byte[] plainkey = encryptedKey.Select(b => (byte)(b - moveBy)).ToArray();
                return plainkey;
            }

            public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] key)
            {
                if (!this.WrapKeyCallsCount.ContainsKey(masterKeyPath))
                {
                    this.WrapKeyCallsCount[masterKeyPath] = 1;
                }
                else
                {
                    this.WrapKeyCallsCount[masterKeyPath]++;
                }

                this.keyinfo.TryGetValue(masterKeyPath, out int moveBy);
                byte[] encryptedkey = key.Select(b => (byte)(b + moveBy)).ToArray();
                return encryptedkey;
            }

            public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
            {
                byte[] rawKey = new byte[32];
                SecurityUtility.GenerateRandomBytes(rawKey);
                return rawKey;
            }

            public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
            {
                return true;
            }
        }

        // This class is same as CosmosEncryptor but copied so as to induce decryption failure easily for testing.
        private class TestEncryptor : Encryptor
        {
            public DataEncryptionKeyProvider DataEncryptionKeyProvider { get; }
            public bool FailDecryption { get; set; }

            public TestEncryptor(DataEncryptionKeyProvider dataEncryptionKeyProvider)
            {
                this.DataEncryptionKeyProvider = dataEncryptionKeyProvider;
                this.FailDecryption = false;
            }

            public override async Task<byte[]> DecryptAsync(
                byte[] cipherText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                if (this.FailDecryption && dataEncryptionKeyId.Equals("failDek"))
                {
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned.");
                }

                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                if (dek == null)
                {
                    throw new InvalidOperationException($"Null {nameof(DataEncryptionKey)} returned from {nameof(this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync)}.");
                }

                return dek.DecryptData(cipherText);
            }

            public override async Task<byte[]> EncryptAsync(
                byte[] plainText,
                string dataEncryptionKeyId,
                string encryptionAlgorithm,
                CancellationToken cancellationToken = default)
            {
                DataEncryptionKey dek = await this.DataEncryptionKeyProvider.FetchDataEncryptionKeyAsync(
                    dataEncryptionKeyId,
                    encryptionAlgorithm,
                    cancellationToken);

                return dek.EncryptData(plainText);
            }
        }        

        internal class EncryptionTestsTokenCredentialFactory : KeyVaultTokenCredentialFactory
        {
            public override async ValueTask<TokenCredential> GetTokenCredentialAsync(Uri keyVaultKeyUri, CancellationToken cancellationToken)
            {
                return await Task.FromResult(new DefaultAzureCredential());
            }        
        }
    }
}
