//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Cosmos.Samples.ReEncryption
{
    internal static class Constants
    {
        public const string DocumentsResourcePropertyName = "Documents";
        public const string DocumentIdPropertyName = "id";
        public const string DocumentRidPropertyName = "_rid";

        // Full Fidelity Change feed is in preview. We do not support this currently.
        public const bool IsFFChangeFeedSupported = false;

        public const string LsnPropertyName = "_lsn";
        public const string MetadataPropertyName = "_metadata";
        public const string OperationTypePropertyName = "operationType";
        public const string PreviousImagePropertyName = "previousImage";
    }
}