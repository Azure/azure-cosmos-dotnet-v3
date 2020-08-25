//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    internal enum StatusCodes
    {
        // Success
        Ok = 200,
        Created = 201,
        Accepted = 202,
        NoContent = 204,
        MultiStatus = 207,
        NotModified = 304,

        // Client error
        StartingErrorCode = 400,

        BadRequest = 400,
        Unauthorized = 401,
        Forbidden = 403,
        NotFound = 404,
        MethodNotAllowed = 405,
        RequestTimeout = 408,
        Conflict = 409,
        Gone = 410,
        PreconditionFailed = 412,
        RequestEntityTooLarge = 413,
        Locked = 423,
        FailedDependency = 424,
        TooManyRequests = 429,
        RetryWith = 449,

        InternalServerError = 500,
        BadGateway = 502,
        ServiceUnavailable = 503,

        //Operation pause and cancel. These are FAKE status codes for QOS logging purpose only.
        OperationPaused = 1200,
        OperationCancelled = 1201
    }

    internal enum SubStatusCodes
    {
        Unknown = 0,

        // 400: Bad Request Substatus
        PartitionKeyMismatch = 1001,
        CrossPartitionQueryNotServable = 1004,
        ScriptCompileError = 0xFFFF,    // From ExecuteStoredProcedure.
        AnotherOfferReplaceOperationIsInProgress = 3205,

        // 410: StatusCodeType_Gone: substatus
        NameCacheIsStale = 1000,
        PartitionKeyRangeGone = 1002,
        CompletingSplit = 1007,
        CompletingPartitionMigration = 1008,
        LeaseNotFound = 1022,

        // 403: Forbidden Substatus.
        WriteForbidden = 3,
        ProvisionLimitReached = 1005,
        DatabaseAccountNotFound = 1008,
        RedundantCollectionPut = 1009,
        SharedThroughputDatabaseQuotaExceeded = 1010,
        SharedThroughputOfferGrowNotNeeded = 1011,
        SharedThroughputDatabaseCollectionCountExceeded = 1019,
        SharedThroughputDatabaseCountExceeded = 1020,

        // 404: LSN in session token is higher
        ReadSessionNotAvailable = 1002,
        OwnerResourceNotFound = 1003,
        ConfigurationNameNotFound = 1004,
        ConfigurationPropertyNotFound = 1005,
        CollectionCreateInProgress = 1013,
        StoreNotReady = 1023,

        // 409: Conflict exception
        ConflictWithControlPlane = 1006,
        DatabaseNameAlreadyExists = 3206,
        ConfigurationNameAlreadyExists = 3207,
        PartitionkeyHashCollisionForId = 3302,

        // 503: Service Unavailable due to region being out of capacity for bindable partitions
        InsufficientBindablePartitions = 1007,
        ComputeFederationNotFound = 1012,
        OperationPaused = 9001,

        //412: PreCondition Failed
        SplitIsDisabled = 2001,
        CollectionsInPartitionGotUpdated = 2002,
        CanNotAcquirePKRangesLock = 2003,
        ResourceNotFound = 2004,
        CanNotAcquireOfferOwnerLock = 2005,
        MigrationIsDisabled = 2006,
        CanNotAcquirePKRangeLock = 2007,
        CanNotAcquirePartitionLock = 2008,
        CanNotAcquireGlobalPartitionMigrationLock = 2009,
        CanNotAcquireFederationPartitionMigrationLock = 2010,
        CanNotAcquireSnapshotOwnerLock = 2005,
        StorageSplitConflictingWithNWayThroughputSplit = 2011,
        MergeIsDisabled = 2012,
		TombstoneRecordsNotFound = 2015, // Tombstone records were not found because they were purged.

        // 500: InternalServerError
        ConfigurationNameNotEmpty = 3001,

        // 429: Request Rate Too Large
        PrepareTimeLimitExceeded = 3207,
        ClientTcpChannelFull = 3208,
        BWTermCountLimitExceeded = 3209,

        // Key Vault Access Client Error Code
        AadClientCredentialsGrantFailure = 4000, // Indicated access to AAD failed to get a token
        AadServiceUnavailable = 4001, // Aad Service is Unavailable
        KeyVaultAuthenticationFailure = 4002, // Indicate the KeyVault doesn't grant permission to the AAD, or the key is disabled.
        KeyVaultKeyNotFound = 4003, // Indicate the Key Vault Key is not found
        KeyVaultServiceUnavailable = 4004, // Key Vault Service is Unavailable
        KeyVaultWrapUnwrapFailure = 4005, // Indicate that Key Vault is unable to Wrap or Unwrap, one possible scenario is KeyVault failed to decoded the encrypted blob using the latest key because customer has rotated the key.
        InvalidKeyVaultKeyURI = 4006, // Indicate the Key Vault Key URI is invalid.
        InvalidInputBytes = 4007, // The input bytes are not in the format of base64.
        KeyVaultInternalServerError = 4008, // Other unknown errors
        KeyVaultDNSNotResolved = 4009, // Key Vault DNS name could not be resolved, mostly due to customer enter incorrect KeyVault name.
        InvalidKeyVaultCertURI = 4010, // Indicate the Key Vault Cert URI is invalid.
        InvalidKeyVaultKeyAndCertURI = 4011, // Indicate the Key Vault Key and Cert URI is invalid.
        

        // Keep in sync with Microsoft.Azure.Cosmos.ServiceFramework.Security.AadAuthentication.AadSubStatusCodes
        // 401 : Unauthorized Exception (User-side errors start with 50)
        MissingAuthHeader = 5000,
        InvalidAuthHeaderFormat = 5001,
        AadAuthDisabled = 5002,
        AadTokenInvalidFormat = 5003,
        AadTokenInvalidSignature = 5004,
        AadTokenNotYetValid = 5005,
        AadTokenExpired = 5006,
        AadTokenInvalidIssuer = 5007,
        AadTokenInvalidAudience = 5008,
        AadTokenInvalidScope = 5009,
        FailedToGetAadToken = 5010,

        // 401 : Unauthorized Exception (CosmosDB-side errors start with 52)
        AadTokenInvalidSigningKey = 5200,
        AadTokenGroupExpansionError = 5201,
    }
}
