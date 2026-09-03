//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests.Utils;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public sealed class MdeGatewayMigrationTests
    {
        internal static GatewayMigrationTestFixture Fixture { get; } = new ();

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext context)
        {
            _ = context;
            await MdeCustomEncryptionTests.InitializeGatewayMigrationFixtureAsync(Fixture);
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            await MdeCustomEncryptionTests.CleanupGatewayMigrationFixtureAsync(Fixture);
        }

        [TestMethod]
        public void EmulatorClientTopology_SharedConfigurationIsDirectAndMigrationFixtureIsGateway()
        {
            using CosmosClient sharedConfigurationClient = MdeCustomEncryptionTests.CreateSharedClient();
            using CosmosClient migrationConfigurationClient = CreateMigrationClient();

            Assert.AreEqual(
                ConnectionMode.Direct,
                sharedConfigurationClient.ClientOptions.ConnectionMode);
            Assert.AreEqual(
                ConnectionMode.Gateway,
                migrationConfigurationClient.ClientOptions.ConnectionMode);
        }

        internal static CosmosClient CreateMigrationClient()
        {
            return TestCommon.CreateCosmosClient(useGateway: true);
        }

        [DataTestMethod]
        [DynamicData(nameof(MigrationProcessorPairs))]
        public async Task PointMigration_HistoricalPreview07LegacyCiphertext_RewritesAsCurrentMde(
            int readerProcessorValue,
            int writerProcessorValue)
        {
            await MdeCustomEncryptionTests.PointMigration_HistoricalPreview07LegacyCiphertext_RewritesAsCurrentMde(
                readerProcessorValue,
                writerProcessorValue);
        }

        [DataTestMethod]
        [DynamicData(nameof(PlaintextMigrationRows))]
        public async Task PointMigration_PlaintextMetadataVariants_ReadUnchangedThenRewriteAsCurrentMde(
            string plaintextState,
            int readerProcessorValue,
            int writerProcessorValue)
        {
            await MdeCustomEncryptionTests.PointMigration_PlaintextMetadataVariants_ReadUnchangedThenRewriteAsCurrentMde(
                plaintextState,
                readerProcessorValue,
                writerProcessorValue);
        }

        [DataTestMethod]
        [DynamicData(nameof(NullEncryptionMetadataMigrationRows))]
        public async Task PointMigration_NullEncryptionMetadata_ReadsUnchangedThenTypedWriteRewritesAsCurrentMde(
            string operation,
            int readerProcessorValue,
            int writerProcessorValue)
        {
            await MdeCustomEncryptionTests.PointMigration_NullEncryptionMetadata_ReadsUnchangedThenTypedWriteRewritesAsCurrentMde(
                operation,
                readerProcessorValue,
                writerProcessorValue);
        }

        [DataTestMethod]
        [DynamicData(nameof(MigrationProcessors))]
        public async Task PointMigration_PresentUnknownAlgorithm_FailsClosedWithoutMigration(
            int readerProcessorValue)
        {
            await MdeCustomEncryptionTests.PointMigration_PresentUnknownAlgorithm_FailsClosedWithoutMigration(
                readerProcessorValue);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public async Task PointWrite_LegacyAlgorithmWithStreamProcessor_RejectsBeforeNetworkDispatch()
        {
            await MdeCustomEncryptionTests.PointWrite_LegacyAlgorithmWithStreamProcessor_RejectsBeforeNetworkDispatch();
        }
#endif

        public static IEnumerable<object[]> MigrationProcessorPairs =>
            MdeCustomEncryptionTests.MigrationProcessorPairs;

        public static IEnumerable<object[]> PlaintextMigrationRows =>
            MdeCustomEncryptionTests.PlaintextMigrationRows;

        public static IEnumerable<object[]> NullEncryptionMetadataMigrationRows =>
            MdeCustomEncryptionTests.NullEncryptionMetadataMigrationRows;

        public static IEnumerable<object[]> MigrationProcessors =>
            MdeCustomEncryptionTests.MigrationProcessors;
    }

    internal sealed class GatewayMigrationTestFixture
    {
        internal CosmosClient Client { get; set; }

        internal Database Database { get; set; }

        internal DataEncryptionKeyProperties DekProperties { get; set; }

        internal Container ItemContainer { get; set; }

        internal Container EncryptionContainer { get; set; }

        internal Container KeyContainer { get; set; }

        internal EncryptionKeyStoreProvider KeyStoreProvider { get; set; }

        internal Encryptor Encryptor { get; set; }

        internal void Reset()
        {
            this.Encryptor = null;
            this.KeyStoreProvider = null;
            this.EncryptionContainer = null;
            this.ItemContainer = null;
            this.KeyContainer = null;
            this.DekProperties = null;
            this.Database = null;
            this.Client = null;
        }
    }
}
