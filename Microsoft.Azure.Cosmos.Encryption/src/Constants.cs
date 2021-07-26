//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class Constants
    {
        public const string DiagnosticsCoreDiagnostics = "CoreDiagnostics";
        public const string DiagnosticsDecryptOperation = "Decrypt";
        public const string DiagnosticsDuration = "Duration in milliseconds";
        public const string DiagnosticsEncryptionDiagnostics = "EncryptionDiagnostics";
        public const string DiagnosticsEncryptOperation = "Encrypt";
        public const string DiagnosticsPropertiesCount = "Total properties count";
        public const string DiagnosticsStartTime = "Start time";
        public const string DocumentsResourcePropertyName = "Documents";
        public const string IncorrectContainerRidSubStatus = "1024";
        public const string SubStatusHeader = "x-ms-substatus";
        public const int SupportedClientEncryptionPolicyFormatVersion = 1;
    }
}