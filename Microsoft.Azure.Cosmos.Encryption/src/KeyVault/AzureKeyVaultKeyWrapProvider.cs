//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.Cosmos.Encryption.KeyVault
{
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
