//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class Constants
    {
        public const string DataEncryptionKeyRid = "_ek";
        public const string EncryptedData = "_ed";
        public const string EncryptedInfo = "_ei";
        public const string EncryptionAlgorithmId = "encryptionAlgorithmId";
        public const string EncryptionFormatVersion = "_ef";
        public const string KeyWrapMetadata = "keyWrapMetadata";
        public const string KeyWrapMetadataType = "type";
        public const string KeyWrapMetadataValue = "value";
        public const string WrappedDataEncryptionKey = "wrappedDataEncryptionKey";
    }
}
