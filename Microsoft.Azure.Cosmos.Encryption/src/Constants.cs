//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption
{
    internal static class Constants
    {
        public const string DocumentsResourcePropertyName = "Documents";
        public const string SubStatusHeader = "x-ms-substatus";
        public const string IncorrectContainerRidSubStatus = "1024";
        public const int SupportedClientEncryptionPolicyFormatVersion = 1;
        public const bool IsFFChangeFeedSupported = false;
    }
}