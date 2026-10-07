//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    internal interface IDataEncryptionKeyBuffer
    {
        int EncryptData(
            byte[] plainText,
            int plainTextOffset,
            int plainTextLength,
            byte[] output,
            int outputOffset);

        int GetEncryptByteCount(int plainTextLength);

        int DecryptData(
            byte[] cipherText,
            int cipherTextOffset,
            int cipherTextLength,
            byte[] output,
            int outputOffset);

        int GetDecryptByteCount(int cipherTextLength);
    }
}
