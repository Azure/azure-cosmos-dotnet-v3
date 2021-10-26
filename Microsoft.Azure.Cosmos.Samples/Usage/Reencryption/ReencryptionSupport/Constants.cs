//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Cosmos.Samples.Reencryption
{
    internal static class Constants
    {
        public const string DocumentsResourcePropertyName = "Documents";
        public const string LsnPropertyName = "_lsn";
        public const string MetadataPropertyName = "_metadata";
        public const string PreviousImagePropertyName = "previousImage";
        public const string DocumentIdPropertyName = "id";
        public const string OperationTypePropertyName = "operationType";
        public const bool IsFFChangeFeedSupported = false;
    }
}