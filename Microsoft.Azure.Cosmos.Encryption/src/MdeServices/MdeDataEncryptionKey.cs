//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    using Microsoft.Data.Encryption.Cryptography;

    internal sealed class MdeDataEncryptionKey : Data.Encryption.Cryptography.DataEncryptionKey
    {
        public MdeDataEncryptionKey(string name, byte[] rootKey)
            : base(name, rootKey)
        {
        }
    }
}