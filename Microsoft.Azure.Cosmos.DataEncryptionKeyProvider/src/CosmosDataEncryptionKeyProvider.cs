//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.DataEncryptionKeyProvider
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction for a provider to get a data encryption key.
    /// See https://aka.ms/CosmosClientEncryption for more information on client-side encryption support in Azure Cosmos DB.
    /// </summary>
    public class CosmosDataEncryptionKeyProvider : Cosmos.DataEncryptionKeyProvider
    {
        internal DekCache DekCache { get; }

        internal Container Container { get; private set; }

        public EncryptionKeyWrapProvider EncryptionKeyWrapProvider { get; }

        public DataEncryptionKeyContainer DataEncryptionKeyContainer { get; }

        public CosmosDataEncryptionKeyProvider(
            EncryptionKeyWrapProvider encryptionKeyWrapProvider,
            TimeSpan? dekPropertiesTimeToLive = null)
        {
            this.EncryptionKeyWrapProvider = encryptionKeyWrapProvider;
            this.DataEncryptionKeyContainer = new DataEncryptionKeyContainerCore(this);
            this.DekCache = new DekCache(dekPropertiesTimeToLive);
        }

        public void Initialize(Container container)
        {
            if(this.Container != null)
            {
                throw new InvalidOperationException($"{nameof(CosmosDataEncryptionKeyProvider)} has already been initialized.");
            }

            this.Container = container;
        }

        public override async Task<byte[]> FetchDataEncryptionKeyAsync(
            string id,
            CancellationToken cancellationToken)
        {
            if(this.Container == null)
            {
                throw new InvalidOperationException($"The {nameof(CosmosDataEncryptionKeyProvider)} was not initialized.");
            }

            DataEncryptionKeyCore dekCore = (DataEncryptionKeyInlineCore)this.DataEncryptionKeyContainer.GetDataEncryptionKey(id);
            (DataEncryptionKeyProperties _, InMemoryRawDek inMemoryRawDek) = await dekCore.FetchUnwrappedAsync(null, cancellationToken);
            return inMemoryRawDek.RawDek;
        }
    }
}
