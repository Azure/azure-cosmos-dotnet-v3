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

        // TODO: Good to have constants available in the Cosmos SDK. Tracked via https://github.com/Azure/azure-cosmos-dotnet-v3/issues/2431
        public const string IntendedCollectionHeader = "x-ms-cosmos-intended-collection-rid";
        public const string IsClientEncryptedHeader = "x-ms-cosmos-is-client-encrypted";
        public const string AllowCachedReadsHeader = "x-ms-cosmos-allow-cachedreads";
        public const string DatabaseRidHeader = "x-ms-cosmos-database-rid";
    }
}