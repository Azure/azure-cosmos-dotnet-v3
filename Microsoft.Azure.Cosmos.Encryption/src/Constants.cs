//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class Constants
    {
        public const string CoreDiagnostics = "CoreDiagnostics";
        public const string DecryptOperation = "Decrypt";
        public const string DiagnosticsPropertiesDecryptedCount = "Total properties decrypted";
        public const string DiagnosticsDuration = "Duration in milliseconds";
        public const string DiagnosticsPropertiesEncryptedCount = "Total properties encrypted";
        public const string DiagnosticsStartTime = "Start time";
        public const string EncryptionDiagnostics = "EncryptionDiagnostics";
        public const string EncryptOperation = "Encrypt";
        public const string DocumentsResourcePropertyName = "Documents";
        public const string IncorrectContainerRidSubStatus = "1024";
        public const string SubStatusHeader = "x-ms-substatus";
        public const int SupportedClientEncryptionPolicyFormatVersion = 1;
    }
}