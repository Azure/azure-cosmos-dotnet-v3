//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Encryption.Custom
{
    internal static class EncryptionDiagnostics
    {
        internal const string ScopeDecryptSelectProcessorPrefix = "EncryptionProcessor.Decrypt.SelectProcessor.";
        internal const string ScopeDecryptStreamingProvidedOutputSelectProcessorPrefix = "EncryptionProcessor.Decrypt.StreamingProvidedOutput.SelectProcessor.";
        internal const string ScopeDecryptStreamImplMde = "EncryptionProcessor.DecryptStreamImpl.Mde";
        internal const string ScopeDeserializeAndDecryptResponseAsync = "EncryptionProcessor.DeserializeAndDecryptResponseAsync";
    }
}
