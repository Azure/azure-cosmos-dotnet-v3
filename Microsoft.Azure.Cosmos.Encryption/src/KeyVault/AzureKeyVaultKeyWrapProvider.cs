//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
    using System.Threading;
    using System.Threading.Tasks;

    internal class AzureKeyVaultKeyWrapProvider : EncryptionKeyWrapProvider
    {
        public AzureKeyVaultKeyWrapProvider(
            string clientId,
            string certificateThumbprint)
        {
        }

        public override Task<EncryptionKeyUnwrapResult> UnwrapKeyAsync(
            byte[] wrappedKey,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        public override Task<EncryptionKeyWrapResult> WrapKeyAsync(
            byte[] key,
            EncryptionKeyWrapMetadata metadata,
            CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }
    }
}
