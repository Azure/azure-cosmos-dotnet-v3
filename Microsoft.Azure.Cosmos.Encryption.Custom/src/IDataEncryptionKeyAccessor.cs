//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IDataEncryptionKeyAccessor
    {
        Task<DataEncryptionKey> GetEncryptionKeyAsync(
            string dataEncryptionKeyId,
            string encryptionAlgorithm,
            CancellationToken cancellationToken);
    }
}
