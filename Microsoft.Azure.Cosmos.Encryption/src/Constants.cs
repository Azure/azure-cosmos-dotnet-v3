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
        public const string DiagnosticsPropertiesEncryptedCount = "Properties Encrypted Count";
        public const string DiagnosticsPropertiesDecryptedCount = "Properties Decrypted Count";
        public const string DiagnosticsStartTime = "Start time";
        public const string DocumentsResourcePropertyName = "Documents";
        public const string IncorrectContainerRidSubStatus = "1024";
        public const string PartitionKeyMismatch = "1001";

        // TODO: Good to have constants available in the Cosmos SDK. Tracked via https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2431
        public const string IntendedCollectionHeader = "x-ms-cosmos-intended-collection-rid";
        public const string IsClientEncryptedHeader = "x-ms-cosmos-is-client-encrypted";
        public const string AllowCachedReadsHeader = "x-ms-cosmos-allow-cachedreads";
        public const string DatabaseRidHeader = "x-ms-cosmos-database-rid";
        public const string SubStatusHeader = "x-ms-substatus";
        public const int SupportedClientEncryptionPolicyFormatVersion = 2;
    }
}