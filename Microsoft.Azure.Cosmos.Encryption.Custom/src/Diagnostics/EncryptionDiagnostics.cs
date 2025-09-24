//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    internal static class EncryptionDiagnostics
    {
        // Unified (preview) diagnostic prefix for selecting the JSON processing mode (Newtonsoft vs Stream)
        internal const string ScopeEncryptModeSelectionPrefix = "EncryptionProcessor.Encrypt.Mde.";
        internal const string ScopeDecryptModeSelectionPrefix = "EncryptionProcessor.Decrypt.Mde.";
        internal const string ScopeDecryptStreamImplMde = "EncryptionProcessor.DecryptStreamImpl.Mde";
        internal const string ScopeDeserializeAndDecryptResponseAsync = "EncryptionProcessor.DeserializeAndDecryptResponseAsync";
    }
}
