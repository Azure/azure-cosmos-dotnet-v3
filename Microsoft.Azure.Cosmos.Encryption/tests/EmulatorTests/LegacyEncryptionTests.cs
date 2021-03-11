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
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core;
    using global::Azure.Identity;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Scripts;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Newtonsoft.Json;
    using static Microsoft.Azure.Cosmos.Encryption.KeyVaultAccessClientTests;
    using EncryptionKeyWrapMetadata = Custom.EncryptionKeyWrapMetadata;

    [TestClass]
    public class LegacyEncryptionTests
    {
        private static readonly EncryptionKeyWrapMetadata metadata1 = new EncryptionKeyWrapMetadata("metadata1");
        private static readonly EncryptionKeyWrapMetadata metadata2 = new EncryptionKeyWrapMetadata("metadata2");
        private const string metadataUpdateSuffix = "updated";
        private static TimeSpan cacheTTL = TimeSpan.FromDays(1);
        private const string dekId = "mydek";
        private static CosmosClient client;
        private static Database database;
        private static DataEncryptionKeyProperties dekProperties;
        private static Container itemContainer;
        private static Container encryptionContainer;
        private static Container keyContainer;
        private static TestKeyWrapProvider testKeyWrapProvider;
        private static CosmosDataEncryptionKeyProvider dekProvider;
        private static TestEncryptor encryptor;

        private static byte[] rawDekForKeyVault;
        private static Uri keyVaultKeyUri;
        private static AzureKeyVaultKeyWrapMetadata azureKeyVaultKeyWrapMetadata;
        private static AzureKeyVaultKeyWrapProvider azureKeyVaultKeyWrapProvider;
        private static EncryptionTestsTokenCredentialFactory encryptionTestsTokenCredentialFactory;

        [ClassInitialize]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "The ClassInitialize method takes a single parameter of type TestContext.")]
        public static async Task ClassInitialize(TestContext context)
        {
            _ = context;
            LegacyEncryptionTests.testKeyWrapProvider = new TestKeyWrapProvider();
            LegacyEncryptionTests.dekProvider = new CosmosDataEncryptionKeyProvider(LegacyEncryptionTests.testKeyWrapProvider);
            LegacyEncryptionTests.encryptor = new TestEncryptor(LegacyEncryptionTests.dekProvider);

            LegacyEncryptionTests.client = TestCommon.CreateCosmosClient();
            LegacyEncryptionTests.database = await LegacyEncryptionTests.client.CreateDatabaseAsync(Guid.NewGuid().ToString());

            LegacyEncryptionTests.keyContainer = await LegacyEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            await LegacyEncryptionTests.dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);

            LegacyEncryptionTests.itemContainer = await LegacyEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/PK", 400);
            LegacyEncryptionTests.encryptionContainer = LegacyEncryptionTests.itemContainer.WithEncryptor(encryptor);
            LegacyEncryptionTests.dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, LegacyEncryptionTests.dekId);

            LegacyEncryptionTests.rawDekForKeyVault = DataEncryptionKey.Generate(CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized);
            LegacyEncryptionTests.encryptionTestsTokenCredentialFactory = new EncryptionTestsTokenCredentialFactory();
            LegacyEncryptionTests.azureKeyVaultKeyWrapProvider = new AzureKeyVaultKeyWrapProvider(encryptionTestsTokenCredentialFactory, new KeyClientTestFactory(), new CryptographyClientFactoryTestFactory());
            keyVaultKeyUri = new Uri("https://testdemo1.vault.azure.net/keys/testkey1/47d306aeaae74baab294672354603ca3");
            LegacyEncryptionTests.azureKeyVaultKeyWrapMetadata = new AzureKeyVaultKeyWrapMetadata(keyVaultKeyUri);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (LegacyEncryptionTests.database != null)
            {
                using (await LegacyEncryptionTests.database.DeleteStreamAsync()) { }
            }

            if (LegacyEncryptionTests.client != null)
            {
                LegacyEncryptionTests.client.Dispose();
            }
        }

        [TestMethod]
        public async Task EncryptionCreateDek()
        {
            string dekId = "anotherDek";
            DataEncryptionKeyProperties dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionCreateDekWithMdeAlgorithmFails()
        {
            string dekId = "mdeDek";
            try
            {
                await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId, CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized);
                Assert.Fail();
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("For use of 'MdeAeadAes256CbcHmac256Randomized' algorithm, Encryptor or CosmosDataEncryptionKeyProvider needs to be initialized with EncryptionKeyStoreProvider.", ex.Message);
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
            EncryptionKeyWrapResult wrappedKey = await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);
            byte[] wrappedDek = wrappedKey.WrappedDataEncryptionKey;
            EncryptionKeyWrapMetadata wrappedKeyVaultMetaData = wrappedKey.EncryptionKeyWrapMetadata;
            
            EncryptionKeyUnwrapResult keyUnwrapResponse = await LegacyEncryptionTests.UnwrapDekKeyVaultAsync(wrappedDek, wrappedKeyVaultMetaData, cancellationToken);

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

            EncryptionKeyWrapResult wrappedKey = await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);
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
            await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Validates handling of Wrapping of Dek.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task WrapKeyUsingKeyVault()
        {
            CancellationToken cancellationToken = default;
            EncryptionKeyWrapResult keyWrapResponse = await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, azureKeyVaultKeyWrapMetadata, cancellationToken);

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
            await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);

        }

        /// <summary>
        /// Integration validation of DEK Provider with KeyVault Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task EncryptionCreateDekKeyVaultWrapProvider()
        {
            string dekId = "DekWithKeyVault";
            DataEncryptionKeyProperties dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);

            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(azureKeyVaultKeyWrapProvider);
            await dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);
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

            await LegacyEncryptionTests.WrapDekKeyVaultAsync(null, azureKeyVaultKeyWrapMetadata, cancellationToken);
        }

        /// <summary>
        /// Integration testing for Rewrapping Dek with KeyVault Wrap Provider.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task EncryptionRewrapDekWithKeyVaultWrapProvider()
        {
            string dekId = "randomDekKeyVault";
            DataEncryptionKeyProperties dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await LegacyEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                LegacyEncryptionTests.metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = LegacyEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata2.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(azureKeyVaultKeyWrapProvider);
            await dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);
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
            await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
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
            await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);
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
            await LegacyEncryptionTests.WrapDekKeyVaultAsync(rawDekForKeyVault, invalidWrapMetadata, cancellationToken);

        }

        [TestMethod]
        public async Task EncryptionRewrapDek()
        {
            string dekId = "randomDek";
            DataEncryptionKeyProperties dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            ItemResponse<DataEncryptionKeyProperties> dekResponse = await LegacyEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                LegacyEncryptionTests.metadata2);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = LegacyEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata2.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionRewrapDekEtagMismatch()
        {
            string dekId = "dummyDek";
            EncryptionKeyWrapMetadata newMetadata = new EncryptionKeyWrapMetadata("newMetadata");

            DataEncryptionKeyProperties dekProperties = await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            // modify dekProperties directly, which would lead to etag change
            DataEncryptionKeyProperties updatedDekProperties = new DataEncryptionKeyProperties(
                dekProperties.Id,
                dekProperties.EncryptionAlgorithm,
                dekProperties.WrappedDataEncryptionKey,
                dekProperties.EncryptionKeyWrapMetadata,
                DateTime.UtcNow);
            await LegacyEncryptionTests.keyContainer.ReplaceItemAsync<DataEncryptionKeyProperties>(
                updatedDekProperties,
                dekProperties.Id,
                new PartitionKey(dekProperties.Id));

            // rewrap should succeed, despite difference in cached value
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await LegacyEncryptionTests.dekProvider.DataEncryptionKeyContainer.RewrapDataEncryptionKeyAsync(
                dekId,
                newMetadata);

            Assert.AreEqual(HttpStatusCode.OK, dekResponse.StatusCode);
            dekProperties = LegacyEncryptionTests.VerifyDekResponse(
                dekResponse,
                dekId);
            Assert.AreEqual(
                new EncryptionKeyWrapMetadata(newMetadata.Value + LegacyEncryptionTests.metadataUpdateSuffix),
                dekProperties.EncryptionKeyWrapMetadata);

            Assert.AreEqual(2, LegacyEncryptionTests.testKeyWrapProvider.WrapKeyCallsCount[newMetadata.Value]);

            // Use different DEK provider to avoid (unintentional) cache impact
            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            await dekProvider.InitializeAsync(LegacyEncryptionTests.database, LegacyEncryptionTests.keyContainer.Id);
            DataEncryptionKeyProperties readProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(dekId);
            Assert.AreEqual(dekProperties, readProperties);
        }

        [TestMethod]
        public async Task EncryptionDekReadFeed()
        {
            Container newKeyContainer = await LegacyEncryptionTests.database.CreateContainerAsync(Guid.NewGuid().ToString(), "/id", 400);
            try
            {
                CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
                await dekProvider.InitializeAsync(LegacyEncryptionTests.database, newKeyContainer.Id);

                string contosoV1 = "Contoso_v001";
                string contosoV2 = "Contoso_v002";
                string fabrikamV1 = "Fabrikam_v001";
                string fabrikamV2 = "Fabrikam_v002";

                await LegacyEncryptionTests.CreateDekAsync(dekProvider, contosoV1);
                await LegacyEncryptionTests.CreateDekAsync(dekProvider, contosoV2);
                await LegacyEncryptionTests.CreateDekAsync(dekProvider, fabrikamV1);
                await LegacyEncryptionTests.CreateDekAsync(dekProvider, fabrikamV2);

                // Test getting all keys
                await LegacyEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2, fabrikamV1, fabrikamV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: false,
                    "SELECT * from c");

                // Test getting specific subset of keys
                await LegacyEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV2 },
                    isExpectedDeksCompleteSetForRequest: false,
                    isResultOrderExpected: true,
                    "SELECT TOP 1 * from c where c.id >= 'Contoso_v000' and c.id <= 'Contoso_v999' ORDER BY c.id DESC");

                // Ensure only required results are returned
                QueryDefinition queryDefinition = new QueryDefinition("SELECT * from c where c.id >= @startId and c.id <= @endId ORDER BY c.id ASC")
                    .WithParameter("@startId", "Contoso_v000")
                    .WithParameter("@endId", "Contoso_v999");
                
                await LegacyEncryptionTests.IterateDekFeedAsync(
                    dekProvider,
                    new List<string> { contosoV1, contosoV2 },
                    isExpectedDeksCompleteSetForRequest: true,
                    isResultOrderExpected: true,
                    query: null,
                    queryDefinition: queryDefinition);

                // Test pagination
                await LegacyEncryptionTests.IterateDekFeedAsync(
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
            ItemResponse<TestDoc> createResponse = await LegacyEncryptionTests.encryptionContainer.CreateItemAsync(
                testDoc,
                new PartitionKey(testDoc.PK));
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.AreEqual(testDoc, createResponse.Resource);
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithNullEncryptionOptions()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<TestDoc> createResponse = await LegacyEncryptionTests.encryptionContainer.CreateItemAsync(
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
                await LegacyEncryptionTests.encryptionContainer.CreateItemAsync(
                    testDoc,
                    requestOptions: LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt));
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
                await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, unknownDek, TestDoc.PathsToEncrypt);
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
            TestDoc testDoc = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            await LegacyEncryptionTests.VerifyItemByReadStreamAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            TestDoc expectedDoc = new TestDoc(testDoc);

            // Read feed (null query)
            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                query: null,
                expectedDoc);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                string.Format(
                    "SELECT * FROM c where c.PK = '{0}' and c.id = '{1}' and c.NonSensitive = '{2}'",
                    expectedDoc.PK,
                    expectedDoc.Id,
                    expectedDoc.NonSensitive),
                expectedDoc);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                string.Format("SELECT * FROM c where c.Sensitive = '{0}'", testDoc.Sensitive),
                expectedDoc: null);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                queryDefinition: new QueryDefinition(
                    "select * from c where c.id = @theId and c.PK = @thePK")
                         .WithParameter("@theId", expectedDoc.Id)
                         .WithParameter("@thePK", expectedDoc.PK),
                expectedDoc: expectedDoc);

            expectedDoc.Sensitive = null;

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.Sensitive, c.NonSensitive FROM c",
                expectedDoc);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                "SELECT c.id, c.PK, c.NonSensitive FROM c",
                expectedDoc);

            await LegacyEncryptionTests.ValidateSprocResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                expectedDoc);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException), "Decryptable content is not initialized.")]
        public void ValidateDecryptableContent()
        {
            TestDoc testDoc = TestDoc.Create();
            EncryptableItem<TestDoc> encryptableItem = new EncryptableItem<TestDoc>(testDoc);
            encryptableItem.DecryptableItem.GetItemAsync<TestDoc>();
        }

        [TestMethod]
        public async Task EncryptionCreateItemWithLazyDecryption()
        {
            TestDoc testDoc = TestDoc.Create();
            ItemResponse<EncryptableItem<TestDoc>> createResponse = await LegacyEncryptionTests.encryptionContainer.CreateItemAsync(
                new EncryptableItem<TestDoc>(testDoc),
                new PartitionKey(testDoc.PK),
                LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            Assert.IsNotNull(createResponse.Resource);

            await LegacyEncryptionTests.ValidateDecryptableItem(createResponse.Resource.DecryptableItem, testDoc);

            // stream
            TestDoc testDoc1 = TestDoc.Create();
            ItemResponse<EncryptableItemStream> createResponseStream = await LegacyEncryptionTests.encryptionContainer.CreateItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc1)),
                new PartitionKey(testDoc1.PK),
                LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, createResponseStream.StatusCode);
            Assert.IsNotNull(createResponseStream.Resource);

            await LegacyEncryptionTests.ValidateDecryptableItem(createResponseStream.Resource.DecryptableItem, testDoc1);
        }

        [TestMethod]
        public async Task EncryptionChangeFeedDecryptionSuccessful()
        {
            string dek2 = "dek2ForChangeFeed";
            await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            
            // change feed iterator
            await this.ValidateChangeFeedIteratorResponse(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2);

            // change feed processor
            // await this.ValidateChangeFeedProcessorResponse(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2);
        }

        [TestMethod]
        public async Task EncryptionHandleDecryptionFailure()
        {
            string dek2 = "failDek";
            await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dek2);

            TestDoc testDoc1 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";

            // success
            await LegacyEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // induce failure for one document
            LegacyEncryptionTests.encryptor.FailDecryption = true;
            testDoc1.Sensitive = null;

            FeedIterator<DecryptableItem> queryResponseIterator = LegacyEncryptionTests.encryptionContainer.GetItemQueryIterator<DecryptableItem>(query);
            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIterator.ReadNextAsync();
            await this.ValidateLazyDecryptionResponse(readDocsLazily, dek2);

            // validate changeFeed handling
            FeedIterator<DecryptableItem> changeIterator = LegacyEncryptionTests.encryptionContainer.GetChangeFeedIterator<DecryptableItem>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            while (changeIterator.HasMoreResults)
            {
                try
                {
                    readDocsLazily = await changeIterator.ReadNextAsync();
                    if (readDocsLazily.Resource != null)
                    {
                        await this.ValidateLazyDecryptionResponse(readDocsLazily, dek2);
                    }
                }
                catch (CosmosException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("Response status code does not indicate success: NotModified (304)"));
                    break;
                }
            }

            // await this.ValidateChangeFeedProcessorResponse(LegacyEncryptionTests.itemContainerCore, testDoc1, testDoc2, false);
            LegacyEncryptionTests.encryptor.FailDecryption = false;
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleDocs()
        {
            TestDoc testDoc1 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            TestDoc testDoc2 = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable
            await LegacyEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2, null);

            string query = $"SELECT * FROM c WHERE c.PK in ('{testDoc1.PK}', '{testDoc2.PK}')";
            await LegacyEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);

            // ORDER BY query
            query += " ORDER BY c._ts";

            await LegacyEncryptionTests.ValidateQueryResultsMultipleDocumentsAsync(LegacyEncryptionTests.encryptionContainer, testDoc1, testDoc2, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryResultMultipleEncryptedProperties()
        {
            List<string> pathsEncrypted = new List<string>() { "/Sensitive", "/NonSensitive" };
            TestDoc testDoc = await LegacyEncryptionTests.CreateItemAsync(
                LegacyEncryptionTests.encryptionContainer,
                LegacyEncryptionTests.dekId,
                pathsEncrypted);

            TestDoc expectedDoc = new TestDoc(testDoc);

            await LegacyEncryptionTests.ValidateQueryResultsAsync(
                LegacyEncryptionTests.encryptionContainer,
                "SELECT * FROM c",
                expectedDoc,
                pathsEncrypted: pathsEncrypted);
        }

        [TestMethod]
        public async Task EncryptionDecryptQueryValueResponse()
        {
            await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            string query = "SELECT VALUE COUNT(1) FROM c";

            await LegacyEncryptionTests.ValidateQueryResponseAsync(LegacyEncryptionTests.encryptionContainer, query);
            await LegacyEncryptionTests.ValidateQueryResponseWithLazyDecryptionAsync(LegacyEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionDecryptGroupByQueryResultTest()
        {
            string partitionKey = Guid.NewGuid().ToString();

            await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);
            await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt, partitionKey);

            string query = $"SELECT COUNT(c.Id), c.PK " +
                           $"FROM c WHERE c.PK = '{partitionKey}' " +
                           $"GROUP BY c.PK ";

            await LegacyEncryptionTests.ValidateQueryResponseAsync(LegacyEncryptionTests.encryptionContainer, query);
        }

        [TestMethod]
        public async Task EncryptionStreamIteratorValidation()
        {
            await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            // test GetItemLinqQueryable with ToEncryptionStreamIterator extension
            await LegacyEncryptionTests.ValidateQueryResponseAsync(LegacyEncryptionTests.encryptionContainer);
        }

        [TestMethod]
        public async Task EncryptionRudItem()
        {
            TestDoc testDoc = await LegacyEncryptionTests.UpsertItemAsync(
                LegacyEncryptionTests.encryptionContainer,
                TestDoc.Create(),
                LegacyEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.Created);

            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<TestDoc> upsertResponse = await LegacyEncryptionTests.UpsertItemAsync(
                LegacyEncryptionTests.encryptionContainer,
                testDoc,
                LegacyEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                HttpStatusCode.OK);
            TestDoc updatedDoc = upsertResponse.Resource;

            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, updatedDoc);

            updatedDoc.NonSensitive = Guid.NewGuid().ToString();
            updatedDoc.Sensitive = Guid.NewGuid().ToString();

            TestDoc replacedDoc = await LegacyEncryptionTests.ReplaceItemAsync(
                LegacyEncryptionTests.encryptionContainer,
                updatedDoc,
                LegacyEncryptionTests.dekId,
                TestDoc.PathsToEncrypt,
                upsertResponse.ETag);

            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, replacedDoc);

            await LegacyEncryptionTests.DeleteItemAsync(LegacyEncryptionTests.encryptionContainer, replacedDoc);
        }

        [TestMethod]
        public async Task EncryptionRudItemLazyDecryption()
        {
            TestDoc testDoc = TestDoc.Create();
            // Upsert (item doesn't exist)
            ItemResponse <EncryptableItem<TestDoc>> upsertResponse = await LegacyEncryptionTests.encryptionContainer.UpsertItemAsync(
                new EncryptableItem<TestDoc>(testDoc),
                new PartitionKey(testDoc.PK),
                LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.Created, upsertResponse.StatusCode);
            Assert.IsNotNull(upsertResponse.Resource);

            await LegacyEncryptionTests.ValidateDecryptableItem(upsertResponse.Resource.DecryptableItem, testDoc);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            // Upsert with stream (item exists)
            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<EncryptableItemStream> upsertResponseStream = await LegacyEncryptionTests.encryptionContainer.UpsertItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc)),
                new PartitionKey(testDoc.PK),
                LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt));

            Assert.AreEqual(HttpStatusCode.OK, upsertResponseStream.StatusCode);
            Assert.IsNotNull(upsertResponseStream.Resource);

            await LegacyEncryptionTests.ValidateDecryptableItem(upsertResponseStream.Resource.DecryptableItem, testDoc);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            // replace
            testDoc.NonSensitive = Guid.NewGuid().ToString();
            testDoc.Sensitive = Guid.NewGuid().ToString();

            ItemResponse<EncryptableItemStream> replaceResponseStream = await LegacyEncryptionTests.encryptionContainer.ReplaceItemAsync(
                new EncryptableItemStream(TestCommon.ToStream(testDoc)),
                testDoc.Id,
                new PartitionKey(testDoc.PK),
                LegacyEncryptionTests.GetRequestOptions(LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt, upsertResponseStream.ETag));

            Assert.AreEqual(HttpStatusCode.OK, replaceResponseStream.StatusCode);
            Assert.IsNotNull(replaceResponseStream.Resource);

            await LegacyEncryptionTests.ValidateDecryptableItem(replaceResponseStream.Resource.DecryptableItem, testDoc);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, testDoc);

            await LegacyEncryptionTests.DeleteItemAsync(LegacyEncryptionTests.encryptionContainer, testDoc);
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthRestricted()
        {
            TestDoc testDoc = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            User restrictedUser = LegacyEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await LegacyEncryptionTests.database.CreateUserAsync(restrictedUser.Id);

            PermissionProperties restrictedUserPermission = await restrictedUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, LegacyEncryptionTests.itemContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);

            CosmosClient clientForRestrictedUser = TestCommon.CreateCosmosClient(
                restrictedUserPermission.Token);

            Database databaseForRestrictedUser = clientForRestrictedUser.GetDatabase(LegacyEncryptionTests.database.Id);
            Container containerForRestrictedUser = databaseForRestrictedUser.GetContainer(LegacyEncryptionTests.itemContainer.Id);			
            Container encryptionContainerForRestrictedUser = containerForRestrictedUser.WithEncryptor(encryptor);

            await LegacyEncryptionTests.PerformForbiddenOperationAsync(() =>
                dekProvider.InitializeAsync(databaseForRestrictedUser, LegacyEncryptionTests.keyContainer.Id), "CosmosDekProvider.InitializeAsync");

            await LegacyEncryptionTests.PerformOperationOnUninitializedDekProviderAsync(() =>
                dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(LegacyEncryptionTests.dekId), "DEK.ReadAsync");

            try
            {
                await encryptionContainerForRestrictedUser.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK));
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(ex.Message, "The CosmosDataEncryptionKeyProvider was not initialized.");
            }

            try
            {
                await encryptionContainerForRestrictedUser.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK));
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual(ex.Message, "The CosmosDataEncryptionKeyProvider was not initialized.");
            }
        }

        [TestMethod]
        public async Task EncryptionResourceTokenAuthAllowed()
        {
            User keyManagerUser = LegacyEncryptionTests.database.GetUser(Guid.NewGuid().ToString());
            await LegacyEncryptionTests.database.CreateUserAsync(keyManagerUser.Id);

            PermissionProperties keyManagerUserPermission = await keyManagerUser.CreatePermissionAsync(
                new PermissionProperties(Guid.NewGuid().ToString(), PermissionMode.All, LegacyEncryptionTests.keyContainer));

            CosmosDataEncryptionKeyProvider dekProvider = new CosmosDataEncryptionKeyProvider(new TestKeyWrapProvider());
            TestEncryptor encryptor = new TestEncryptor(dekProvider);
            CosmosClient clientForKeyManagerUser = TestCommon.CreateCosmosClient(keyManagerUserPermission.Token);

            Database databaseForKeyManagerUser = clientForKeyManagerUser.GetDatabase(LegacyEncryptionTests.database.Id);

            await dekProvider.InitializeAsync(databaseForKeyManagerUser, LegacyEncryptionTests.keyContainer.Id);

            DataEncryptionKeyProperties readDekProperties = await dekProvider.DataEncryptionKeyContainer.ReadDataEncryptionKeyAsync(LegacyEncryptionTests.dekId);
            Assert.AreEqual(LegacyEncryptionTests.dekProperties, readDekProperties);
        }

        [TestMethod]
        public async Task EncryptionRestrictedProperties()
        {
            try
            {
                await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, new List<string>() { "/id" });
                Assert.Fail("Expected item creation with id specified to be encrypted to fail.");
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual("PathsToEncrypt includes a invalid path: '/id'.", ex.Message);
            }


            try
            {
                await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, new List<string>() { "/PK" });
                Assert.Fail("Expected item creation with PK specified to be encrypted to fail.");
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
            }
        }

        [TestMethod]
        public async Task EncryptionBulkCrud()
        {
            TestDoc docToReplace = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToReplace.NonSensitive = Guid.NewGuid().ToString();
            docToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToUpsert = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);
            docToUpsert.NonSensitive = Guid.NewGuid().ToString();
            docToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt);

            CosmosClient clientWithBulk = TestCommon.CreateCosmosClient(builder => builder
                .WithBulkExecution(true)
                .Build());

            Database databaseWithBulk = clientWithBulk.GetDatabase(LegacyEncryptionTests.database.Id);
            Container containerWithBulk = databaseWithBulk.GetContainer(LegacyEncryptionTests.itemContainer.Id);
            Container encryptionContainerWithBulk = containerWithBulk.WithEncryptor(LegacyEncryptionTests.encryptor);

            List<Task> tasks = new List<Task>()
            {
                LegacyEncryptionTests.CreateItemAsync(encryptionContainerWithBulk, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                LegacyEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, TestDoc.Create(), LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.Created),
                LegacyEncryptionTests.ReplaceItemAsync(encryptionContainerWithBulk, docToReplace, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt),
                LegacyEncryptionTests.UpsertItemAsync(encryptionContainerWithBulk, docToUpsert, LegacyEncryptionTests.dekId, TestDoc.PathsToEncrypt, HttpStatusCode.OK),
                LegacyEncryptionTests.DeleteItemAsync(encryptionContainerWithBulk, docToDelete)
            };

            await Task.WhenAll(tasks);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchCrud()
        {
            string partitionKey = "thePK";
            string dek1 = LegacyEncryptionTests.dekId;
            string dek2 = "dek2Forbatch";
            await LegacyEncryptionTests.CreateDekAsync(LegacyEncryptionTests.dekProvider, dek2);

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc2ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc3ToCreate = TestDoc.Create(partitionKey);
            TestDoc doc4ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToReplace = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc2ToReplace.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc1ToUpsert = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek2, TestDoc.PathsToEncrypt, partitionKey);
            doc1ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc1ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc doc2ToUpsert = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            doc2ToUpsert.NonSensitive = Guid.NewGuid().ToString();
            doc2ToUpsert.Sensitive = Guid.NewGuid().ToString();

            TestDoc docToDelete = await LegacyEncryptionTests.CreateItemAsync(LegacyEncryptionTests.encryptionContainer, dek1, TestDoc.PathsToEncrypt, partitionKey);

            TransactionalBatchResponse batchResponse = await LegacyEncryptionTests.encryptionContainer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, LegacyEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .CreateItemStream(doc2ToCreate.ToStream(), LegacyEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, LegacyEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .CreateItem(doc3ToCreate)
                .CreateItem(doc4ToCreate, LegacyEncryptionTests.GetBatchItemRequestOptions(dek1, new List<string>())) // empty PathsToEncrypt list
                .ReplaceItemStream(doc2ToReplace.Id, doc2ToReplace.ToStream(), LegacyEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .UpsertItem(doc1ToUpsert, LegacyEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .DeleteItem(docToDelete.Id)
                .UpsertItemStream(doc2ToUpsert.ToStream(), LegacyEncryptionTests.GetBatchItemRequestOptions(dek2, TestDoc.PathsToEncrypt))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            Assert.AreEqual(doc1ToCreate, doc1.Resource);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            Assert.AreEqual(doc2ToCreate, doc2.Resource);

            TransactionalBatchOperationResult<TestDoc> doc3 = batchResponse.GetOperationResultAtIndex<TestDoc>(2);
            Assert.AreEqual(doc1ToReplace, doc3.Resource);

            TransactionalBatchOperationResult<TestDoc> doc4 = batchResponse.GetOperationResultAtIndex<TestDoc>(3);
            Assert.AreEqual(doc3ToCreate, doc4.Resource);

            TransactionalBatchOperationResult<TestDoc> doc5 = batchResponse.GetOperationResultAtIndex<TestDoc>(4);
            Assert.AreEqual(doc4ToCreate, doc5.Resource);

            TransactionalBatchOperationResult<TestDoc> doc6 = batchResponse.GetOperationResultAtIndex<TestDoc>(5);
            Assert.AreEqual(doc2ToReplace, doc6.Resource);

            TransactionalBatchOperationResult<TestDoc> doc7 = batchResponse.GetOperationResultAtIndex<TestDoc>(6);
            Assert.AreEqual(doc1ToUpsert, doc7.Resource);

            TransactionalBatchOperationResult<TestDoc> doc8 = batchResponse.GetOperationResultAtIndex<TestDoc>(8);
            Assert.AreEqual(doc2ToUpsert, doc8.Resource);

            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc1ToCreate);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc2ToCreate, dekId: dek2);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc3ToCreate, isDocDecrypted: false);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc4ToCreate, isDocDecrypted: false);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc1ToReplace, dekId: dek2);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc2ToReplace, dekId: dek2);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc1ToUpsert);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.encryptionContainer, doc2ToUpsert, dekId: dek2);

            ResponseMessage readResponseMessage = await LegacyEncryptionTests.encryptionContainer.ReadItemStreamAsync(docToDelete.Id, new PartitionKey(docToDelete.PK));
            Assert.AreEqual(HttpStatusCode.NotFound, readResponseMessage.StatusCode);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc1ToCreate);

            doc2ToCreate.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc2ToCreate);

            // doc3ToCreate, doc4ToCreate wasn't encrypted
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc3ToCreate);
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc4ToCreate);

            doc1ToReplace.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc1ToReplace);

            doc2ToReplace.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc2ToReplace);

            doc1ToUpsert.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc1ToUpsert);

            doc2ToUpsert.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc2ToUpsert);
        }

        [TestMethod]
        public async Task EncryptionTransactionalBatchWithCustomSerializer()
        {
            CustomSerializer customSerializer = new CustomSerializer();
            CosmosClient clientWithCustomSerializer = TestCommon.CreateCosmosClient(builder => builder
                .WithCustomSerializer(customSerializer)
                .Build());

            Database databaseWithCustomSerializer = clientWithCustomSerializer.GetDatabase(LegacyEncryptionTests.database.Id);
            Container containerWithCustomSerializer = databaseWithCustomSerializer.GetContainer(LegacyEncryptionTests.itemContainer.Id);
            Container encryptionContainerWithCustomSerializer = containerWithCustomSerializer.WithEncryptor(LegacyEncryptionTests.encryptor);

            string partitionKey = "thePK";
            string dek1 = LegacyEncryptionTests.dekId;

            TestDoc doc1ToCreate = TestDoc.Create(partitionKey);

            ItemResponse<TestDoc> doc1ToReplaceCreateResponse = await LegacyEncryptionTests.CreateItemAsync(encryptionContainerWithCustomSerializer, dek1, TestDoc.PathsToEncrypt, partitionKey);
            TestDoc doc1ToReplace = doc1ToReplaceCreateResponse.Resource;
            doc1ToReplace.NonSensitive = Guid.NewGuid().ToString();
            doc1ToReplace.Sensitive = Guid.NewGuid().ToString();

            TransactionalBatchResponse batchResponse = await encryptionContainerWithCustomSerializer.CreateTransactionalBatch(new Cosmos.PartitionKey(partitionKey))
                .CreateItem(doc1ToCreate, LegacyEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt))
                .ReplaceItem(doc1ToReplace.Id, doc1ToReplace, LegacyEncryptionTests.GetBatchItemRequestOptions(dek1, TestDoc.PathsToEncrypt, doc1ToReplaceCreateResponse.ETag))
                .ExecuteAsync();

            Assert.AreEqual(HttpStatusCode.OK, batchResponse.StatusCode);
            // FromStream is called as part of CreateItem request
            Assert.AreEqual(1, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc1 = batchResponse.GetOperationResultAtIndex<TestDoc>(0);
            Assert.AreEqual(doc1ToCreate, doc1.Resource);
            Assert.AreEqual(2, customSerializer.FromStreamCalled);

            TransactionalBatchOperationResult<TestDoc> doc2 = batchResponse.GetOperationResultAtIndex<TestDoc>(1);
            Assert.AreEqual(doc1ToReplace, doc2.Resource);
            Assert.AreEqual(3, customSerializer.FromStreamCalled);

            await LegacyEncryptionTests.VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToCreate);
            await LegacyEncryptionTests.VerifyItemByReadAsync(encryptionContainerWithCustomSerializer, doc1ToReplace);

            // Validate that the documents are encrypted as expected by trying to retrieve through regular (non-encryption) container
            doc1ToCreate.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc1ToCreate);

            doc1ToReplace.Sensitive = null;
            await LegacyEncryptionTests.VerifyItemByReadAsync(LegacyEncryptionTests.itemContainer, doc1ToReplace);
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

            Assert.AreEqual(expectedDoc, sprocResponse.Resource);
        }

        // One of query or queryDefinition is to be passed in non-null
        private static async Task ValidateQueryResultsAsync(
            Container container,
            string query = null,
            TestDoc expectedDoc = null,
            QueryDefinition queryDefinition = null,
            List<string> pathsEncrypted = null)
        {
            QueryRequestOptions requestOptions = expectedDoc != null
                ? new QueryRequestOptions()
                {
                    PartitionKey = new PartitionKey(expectedDoc.PK),
                }
                : null;

            FeedIterator<TestDoc> queryResponseIterator;
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption;
            if (query != null)
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query, requestOptions: requestOptions);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query, requestOptions: requestOptions);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(queryDefinition, requestOptions: requestOptions);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(queryDefinition, requestOptions: requestOptions);
            }
			
            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);

            if (expectedDoc != null)
            {
                Assert.AreEqual(1, readDocs.Count);
                TestDoc readDoc = readDocs.Single();
                Assert.AreEqual(expectedDoc, readDoc);

                Assert.AreEqual(1, readDocsLazily.Count);
                await LegacyEncryptionTests.ValidateDecryptableItem(readDocsLazily.First(), expectedDoc, pathsEncrypted: pathsEncrypted);
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
            string query)
        {
            FeedIterator<TestDoc> queryResponseIterator;
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption;

            if (query == null)
            {
                IOrderedQueryable<TestDoc> linqQueryable = container.GetItemLinqQueryable<TestDoc>();
                queryResponseIterator = container.ToEncryptionFeedIterator<TestDoc>(linqQueryable);

                IOrderedQueryable<DecryptableItem> linqQueryableDecryptableItem = container.GetItemLinqQueryable<DecryptableItem>();
                queryResponseIteratorForLazyDecryption = container.ToEncryptionFeedIterator<DecryptableItem>(linqQueryableDecryptableItem);
            }
            else
            {
                queryResponseIterator = container.GetItemQueryIterator<TestDoc>(query);
                queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query);
            }

            FeedResponse<TestDoc> readDocs = await queryResponseIterator.ReadNextAsync();
            Assert.AreEqual(null, readDocs.ContinuationToken);

            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);

            if (query == null)
            {
                Assert.IsTrue(readDocs.Count >= 2);
                Assert.IsTrue(readDocsLazily.Count >= 2);
            }
            else
            {
                Assert.AreEqual(2, readDocs.Count);
                Assert.AreEqual(2, readDocsLazily.Count);
            }

            for (int index = 0; index < readDocs.Count; index++)
            {
                if (readDocs.ElementAt(index).Id.Equals(testDoc1.Id))
                {
                    Assert.AreEqual(readDocs.ElementAt(index), testDoc1);
                }
                else if (readDocs.ElementAt(index).Id.Equals(testDoc2.Id))
                {
                    Assert.AreEqual(readDocs.ElementAt(index), testDoc2);
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

        private static async Task ValidateQueryResponseWithLazyDecryptionAsync(Container container,
            string query = null)
        {
            FeedIterator<DecryptableItem> queryResponseIteratorForLazyDecryption = container.GetItemQueryIterator<DecryptableItem>(query);
            FeedResponse<DecryptableItem> readDocsLazily = await queryResponseIteratorForLazyDecryption.ReadNextAsync();
            Assert.AreEqual(null, readDocsLazily.ContinuationToken);
            Assert.AreEqual(1, readDocsLazily.Count);
            (dynamic readDoc, DecryptionContext decryptionContext) = await readDocsLazily.First().GetItemAsync<dynamic>();
            Assert.IsTrue((long)readDoc >= 1);
            Assert.IsNull(decryptionContext);
        }

        private async Task ValidateChangeFeedIteratorResponse(
            Container container,
            TestDoc testDoc1,
            TestDoc testDoc2)
        {
            FeedIterator<TestDoc> changeIterator = container.GetChangeFeedIterator<TestDoc>(
                ChangeFeedStartFrom.Beginning(),
                ChangeFeedMode.Incremental);

            List<TestDoc> changeFeedReturnedDocs = new List<TestDoc>();
            while (changeIterator.HasMoreResults)
            {
                try
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
                catch (CosmosException ex)
                {
                    Assert.IsTrue(ex.Message.Contains("Response status code does not indicate success: NotModified (304)"));
                    break;
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

        private async Task ValidateLazyDecryptionResponse(
            FeedResponse<DecryptableItem> readDocsLazily,
            string failureDek)
        {
            int decryptedDoc = 0;
            int failedDoc = 0;

            foreach (DecryptableItem doc in readDocsLazily)
            {
                try
                {
                    (_, _) = await doc.GetItemAsync<dynamic>();
                    decryptedDoc++;
                }
                catch (EncryptionException encryptionException)
                {
                    failedDoc++;
                    this.ValidateEncryptionException(encryptionException, failureDek);
                }
            }

            Assert.IsTrue(decryptedDoc >= 1);
            Assert.AreEqual(1, failedDoc);
        }

        private void ValidateEncryptionException(
            EncryptionException encryptionException,
            string failureDek)
        {
            Assert.AreEqual(failureDek, encryptionException.DataEncryptionKeyId);
            Assert.IsNotNull(encryptionException.EncryptedContent);
            Assert.IsNotNull(encryptionException.InnerException);
            Assert.IsTrue(encryptionException.InnerException is InvalidOperationException);
            Assert.AreEqual(encryptionException.InnerException.Message, "Null DataEncryptionKey returned.");
        }

        private static async Task IterateDekFeedAsync(
            CosmosDataEncryptionKeyProvider dekProvider,
            List<string> expectedDekIds,
            bool isExpectedDeksCompleteSetForRequest,
            bool isResultOrderExpected,
            string query,
            int? itemCountInPage = null,
            QueryDefinition queryDefinition = null)
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

            FeedIterator<DataEncryptionKeyProperties> dekIterator = queryDefinition != null
                ? dekProvider.DataEncryptionKeyContainer.GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
                    queryDefinition,
                    requestOptions: requestOptions)
                : dekProvider.DataEncryptionKeyContainer.GetDataEncryptionKeyQueryIterator<DataEncryptionKeyProperties>(
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
                LegacyEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
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
                LegacyEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt));
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
                LegacyEncryptionTests.GetRequestOptions(dekId, pathsToEncrypt, etag));

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
                EncryptionOptions = LegacyEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
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
                EncryptionOptions = LegacyEncryptionTests.GetEncryptionOptions(dekId, pathsToEncrypt),
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

        private static async Task ValidateDecryptableItem(
            DecryptableItem decryptableItem,
            TestDoc testDoc,
            string dekId = null,
            List<string> pathsEncrypted = null,
            bool isDocDecrypted = true)
        {
            (TestDoc readDoc, DecryptionContext decryptionContext) = await decryptableItem.GetItemAsync<TestDoc>();
            Assert.AreEqual(testDoc, readDoc);
            if (isDocDecrypted && testDoc.Sensitive != null)
            {
                LegacyEncryptionTests.ValidateDecryptionContext(decryptionContext, dekId, pathsEncrypted);
            }
            else
            {
                Assert.IsNull(decryptionContext);
            }
        }

        private static void ValidateDecryptionContext(
            DecryptionContext decryptionContext,
            string dekId = null,
            List<string> pathsEncrypted = null)
        {
            Assert.IsNotNull(decryptionContext.DecryptionInfoList);
            Assert.AreEqual(1, decryptionContext.DecryptionInfoList.Count);
            DecryptionInfo decryptionInfo = decryptionContext.DecryptionInfoList.First();
            Assert.AreEqual(dekId ?? LegacyEncryptionTests.dekId, decryptionInfo.DataEncryptionKeyId);

            if (pathsEncrypted == null)
            {
                pathsEncrypted = TestDoc.PathsToEncrypt;
            }

            Assert.AreEqual(pathsEncrypted.Count, decryptionInfo.PathsDecrypted.Count);
            Assert.IsFalse(pathsEncrypted.Exists(path => !decryptionInfo.PathsDecrypted.Contains(path)));
        }

        private static async Task VerifyItemByReadStreamAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null)
        {
            ResponseMessage readResponseMessage = await container.ReadItemStreamAsync(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponseMessage.StatusCode);
            Assert.IsNotNull(readResponseMessage.Content);
            TestDoc readDoc = TestCommon.FromStream<TestDoc>(readResponseMessage.Content);
            Assert.AreEqual(testDoc, readDoc);
        }

        private static async Task VerifyItemByReadAsync(Container container, TestDoc testDoc, ItemRequestOptions requestOptions = null, string dekId = null, bool isDocDecrypted = true)
        {
            ItemResponse<TestDoc> readResponse = await container.ReadItemAsync<TestDoc>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
            Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
            Assert.AreEqual(testDoc, readResponse.Resource);

            // ignore for reads via regular container..
            if (container == LegacyEncryptionTests.encryptionContainer)
            {
                ItemResponse<DecryptableItem> readResponseDecryptableItem = await container.ReadItemAsync<DecryptableItem>(testDoc.Id, new PartitionKey(testDoc.PK), requestOptions);
                Assert.AreEqual(HttpStatusCode.OK, readResponse.StatusCode);
                await LegacyEncryptionTests.ValidateDecryptableItem(readResponseDecryptableItem.Resource, testDoc, dekId, isDocDecrypted: isDocDecrypted);
            }
        }

        private static async Task<DataEncryptionKeyProperties> CreateDekAsync(CosmosDataEncryptionKeyProvider dekProvider, string dekId, string algorithm = null)
        {
            ItemResponse<DataEncryptionKeyProperties> dekResponse = await dekProvider.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(
                dekId,
                algorithm ?? CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                LegacyEncryptionTests.metadata1);

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

        private class TestKeyWrapProvider : EncryptionKeyWrapProvider
        {
            public Dictionary<string, int> WrapKeyCallsCount { get; private set; }

            public TestKeyWrapProvider()
            {
                this.WrapKeyCallsCount = new Dictionary<string, int>();
            }

            public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(byte[] wrappedKey, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                int moveBy = metadata.Value == LegacyEncryptionTests.metadata1.Value + LegacyEncryptionTests.metadataUpdateSuffix ? 1 : 2;
                return Task.FromResult(new EncryptionKeyUnwrapResult(wrappedKey.Select(b => (byte)(b - moveBy)).ToArray(), LegacyEncryptionTests.cacheTTL));
            }

            public override Task<EncryptionKeyWrapResult> WrapKeyAsync(byte[] key, EncryptionKeyWrapMetadata metadata, CancellationToken cancellationToken)
            {
                if (!this.WrapKeyCallsCount.ContainsKey(metadata.Value))
                {
                    this.WrapKeyCallsCount[metadata.Value] = 1;
                }
                else
                {
                    this.WrapKeyCallsCount[metadata.Value]++;
                }
                
                EncryptionKeyWrapMetadata responseMetadata = new EncryptionKeyWrapMetadata(metadata.Value + LegacyEncryptionTests.metadataUpdateSuffix);
                int moveBy = metadata.Value == LegacyEncryptionTests.metadata1.Value ? 1 : 2;
                return Task.FromResult(new EncryptionKeyWrapResult(key.Select(b => (byte)(b + moveBy)).ToArray(), responseMetadata));
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

        internal class CustomSerializer : CosmosSerializer
        {
            private readonly JsonSerializer serializer = new JsonSerializer();
            public int FromStreamCalled = 0;

            public override T FromStream<T>(Stream stream)
            {
                this.FromStreamCalled++;
                using (StreamReader sr = new StreamReader(stream))
                using (JsonReader reader = new JsonTextReader(sr))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    return this.serializer.Deserialize<T>(reader);
                }
            }

            public override Stream ToStream<T>(T input)
            {
                MemoryStream streamPayload = new MemoryStream();
                using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: UTF8Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Newtonsoft.Json.Formatting.None;
                        this.serializer.Serialize(writer, input);
                        writer.Flush();
                        streamWriter.Flush();
                    }
                }

                streamPayload.Position = 0;
                return streamPayload;
            }
        }
    }
}
