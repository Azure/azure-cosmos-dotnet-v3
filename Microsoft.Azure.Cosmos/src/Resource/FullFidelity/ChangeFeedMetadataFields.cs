//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.FullFidelity
{
    internal class ChangeFeedMetadataFields
    {
        public const string ConflictResolutionTimestamp = "crts";
        public const string Lsn = "lsn";
        public const string OperationType = "operationType";
        public const string PreviousImageLSN = "previousImageLSN";
        public const string TimeToLiveExpired = "timeToLiveExpired";
    }
}
