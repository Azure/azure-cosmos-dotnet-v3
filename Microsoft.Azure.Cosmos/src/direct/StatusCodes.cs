//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Microsoft.Azure.Documents
{
    internal enum StatusCodes
    {
        Processing = 102,

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
        TooManyRequests = 429,

        // 400: Bad Request Substatus
        PartitionKeyMismatch = 1001,
        CrossPartitionQueryNotServable = 1004,
        ScriptCompileError = 0xFFFF,    // From ExecuteStoredProcedure.
        AnotherOfferReplaceOperationIsInProgress = 3205,
        HttpListenerException = 1101,

        // 410: StatusCodeType_Gone: substatus
        NameCacheIsStale = 1000,
        PartitionKeyRangeGone = 1002,
        CompletingSplit = 1007,
        CompletingPartitionMigration = 1008,
        LeaseNotFound = 1022,
        ArchivalPartitionNotPresent = 1024,

        // 404: LSN in session token is higher
        ReadSessionNotAvailable = 1002,
        OwnerResourceNotFound = 1003,
        ConfigurationNameNotFound = 1004,
        ConfigurationPropertyNotFound = 1005,
        CollectionCreateInProgress = 1013,
        StoreNotReady = 1023,
        AuthTokenNotFoundInCache = 1030,

        // 404: StatusCodeType_NotFound: substatus
        PartitionMigratingCollectionDeleted = 1031,
        PartitionMigrationSourcePartitionDeletedInMaster = 1034,
        PartitionMigrationSharedThroughputDatabasePartitionResourceNotFoundInMaster = 1035,
        PartitionMigrationPartitionResourceNotFoundInMaster = 1036,
        PartitionMigrationFailedToUpdateDNS = 1037,

        // 403: Forbidden Substatus.
        WriteForbidden = 3,
        ProvisionLimitReached = 1005,
        DatabaseAccountNotFound = 1008,
        RedundantCollectionPut = 1009,
        SharedThroughputDatabaseQuotaExceeded = 1010,
        SharedThroughputOfferGrowNotNeeded = 1011,
        PartitionKeyQuotaOverLimit = 1014,
        SharedThroughputDatabaseCollectionCountExceeded = 1019,
        SharedThroughputDatabaseCountExceeded = 1020,
        ComputeInternalError = 1021,
        ThroughputCapQuotaExceeded = 1028,
        InvalidThroughputCapValue = 1029,

        // 409: Conflict exception
        ConflictWithControlPlane = 1006,
        DatabaseNameAlreadyExists = 3206,
        ConfigurationNameAlreadyExists = 3207,
        PartitionkeyHashCollisionForId = 3302,

        // 409: Partition migration Count mismatch conflict sub status codes
        PartitionMigrationDocumentCountMismatchBetweenSourceAndTargetPartition = 3050,
        PartitionMigrationDocumentCountMismatchBetweenTargetPartitionReplicas = 3051,

        // 503: Service Unavailable due to region being out of capacity for bindable partitions
        InsufficientBindablePartitions = 1007,
        ComputeFederationNotFound = 1012,
        OperationPaused = 9001,
        InsufficientCapacity = 9003,

        // Federation Buildout / Expansion error codes
        AggregatedHealthStateError = 6001, // - Aggregated health state is Error
        ApplicationHealthStateError = 6002, // - Any application is in Warning or Error state
        HealthStateError = 6003, // - Any health events in Error state are found
        UnhealthyEventFound = 6004, // - Any unhealthy evaluations are found (except for Warning evaluations on Nodes)
        ClusterHealthEmpty = 6005, // Cluster Health States is empty
        AllocationFailed = 6006, // Allocation failed for federation
        OperationResultNull = 6007, // Null operation result
        OperationResultUnexpected = 6008, // Null operation result

        //412: PreCondition Failed
        SplitIsDisabled = 2001,
        CollectionsInPartitionGotUpdated = 2002,
        CanNotAcquirePKRangesLock = 2003,
        ResourceNotFound = 2004,
        CanNotAcquireOfferOwnerLock = 2005,
        CanNotAcquirePKRangeLock = 2007,
        CanNotAcquirePartitionLock = 2008,
        CanNotAcquireSnapshotOwnerLock = 2005,
        StorageSplitConflictingWithNWayThroughputSplit = 2011,
        MergeIsDisabled = 2012,
        TombstoneRecordsNotFound = 2015, // Tombstone records were not found because they were purged.
        InvalidAccountStatus = 2016,
        OfferValidationFailed = 2017,
        CanNotAquireMasterPartitionAccessLock = 2018,
        CanNotAcquireInAccountRestoreInProgressLock = 2019,
        CollectionStateChanged = 2020,
        OfferScaledUpByUser = 2021,

        //412: PreConditionFailed migration substatus codes
        PartitionMigrationCancelledForPendingUserOperation = 2006,
        PartitionMigrationCanNotAcquireGlobalPartitionMigrationLock = 2009,
        PartitionMigrationCanNotAcquireFederationPartitionMigrationLock = 2010,
        PartitionMigrationServiceTypeAndOperationTypeDoesnotMatch = 2020,
        PartitionMigrationGlobalDatabaseAccountResourceNotFound = 2021,
        PartitionMigrationMasterFederationForWriteRegionNotFound = 2022,
        PartitionMigrationMasterFederationForCurrentRegionNotFound = 2023,
        PartitionMigrationSourceAndTargetFederationSubregionIsNotSame = 2024,
        PartitionMigrationFailedToCreatePartitionMigrationLocks = 2025,
        PartitionMigrationFailedToResolvePartitionInformation = 2026,
        PartitionMigrationIsDisableOnTheGlobalDatabaseAccount = 2028,
        PartitionMigrationIsDisableOnTheRunnerAccount = 2029,
        PartitionMigrationCanNotProceedForInactiveRegionalDatabaseAccount = 2030,
        PartitionMigrationDidNotCompleteWaitForFullSyncInTenRetries = 2031,
        PartitionMigrationCanNotProceedForDeletingRegionalDatabaseAccount = 2032,
        PartitionMigrationCanNotProceedForDeletionFailedRegionalDatabaseAccount = 2033,
        PartitionMigrationCanNotAcquireRegionalDatabaseAccountPartitionMigrationLock = 2034,
        PartitionMigrationIsDisabledOnReadyForDecommissionFederation = 2035,
        PartitionMigrationCanNotAcquirePartitionLock = 2036,
        PartitionMigrationCanNotAcquirePartitionKeyRangesLock = 2037,
        PartitionMigrationCanNotProceedForDeletingGlobalDatabaseAccount = 2038,
        PartitionMigrationCanNotProceedForDeletionFailedGlobalDatabaseAccount = 2039,
        PartitionMigrationCanNotProceedForRevokedGlobalDatabaseAccount = 2040,
        PartitionMigrationMasterServiceTopologyHasWriteRegionEmpty = 2041,
        PartitionMigrationWriteRegionServiceTopologyHasWriteRegionEmpty = 2042,
        PartitionMigrationIsDisabledOnFinalizingDecommissionFederation = 2043,

        // 500: InternalServerError
        ConfigurationNameNotEmpty = 3001,
        ConfigurationOperationCancelled = 3002,
        InvalidAccountConfiguration = 3003,
        FederationDoesnotExistOrIsLocked = 3004,
        PartitionFailoverErrorCode = 3010,

        // 429: Request Rate Too Large
        PrepareTimeLimitExceeded = 3207,
        ClientTcpChannelFull = 3208,
        BWTermCountLimitExceeded = 3209,
        RUBudgetExceeded = 3200,
        GatewayThrottled = 3201,
        StoredProcedureConcurrency = 3084,

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
        CustomerKeyRotated = 4012, // Indicates the rewrapped key doesn't match with existing key.
        MissingRequestParameter = 4013, // Indicates that the incoming request has missing parameters.
        InvalidKeyVaultSecretURI = 4014, // Indicates the Key Vault secret URI is invalid.
        UndefinedDefaultIdentity = 4015, // Indicates that the account has an undefined default identity.
        NspOutboundDenied = 4016, // Indicates that the account's NSP is blocking outbound requests to Key Vault.
        KeyVaultNotFound = 4017, // Indicates that the Key Vault could not be found by the system.

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
        AadTokenMissingObjectIdentifier = 5011,

        SasTokenAuthDisabled = 5012,

        // 401 : Unauthorized Exception (CosmosDB-side errors start with 52)
        AadTokenInvalidSigningKey = 5200,
        AadTokenGroupExpansionError = 5201,
        LocalAuthDisabled = 5202,

        // 403 Forbidden. Blocked by RBAC authorization.
        RbacOperationNotSupported = 5300,
        RbacUnauthorizedMetadataRequest = 5301,
        RbacUnauthorizedNameBasedDataRequest = 5302,
        RbacUnauthorizedRidBasedDataRequest = 5303,
        RbacRidCannotBeResolved = 5304,
        RbacMissingUserId = 5305,
        RbacMissingAction = 5306,

        // 403 Forbidden. (CosmosDB-side errors start with 54)
        RbacRequestWasNotAuthorized = 5400,

        // 403 Forbidden. (NSP related errors)
        NspInboundDenied = 5307,
        NspAuthorizationFailed = 5308,
        NspNoResult = 5309,
        NspInvalidParam = 5310,
        NspInvalidEvalResult = 5311,
        NspNotInitiated = 5312,
        NspOperationNotSupported = 5313,

        // 200 OK. List feed throttled response.
        ListResourceFeedThrottled = 5500,

        // 401 Unauthorized Exception (mutual TLS client auth failed)
        MutualTlsClientAuthFailed = 5600,

        // 200 OK.GW response to GET DocService request.
        LocationsModified = 5700, // Indicates locations derived from topology have been modified due to region being offlined.

        // SDK Codes (Client)
        // IMPORTANT - keep these consistent with Java SDK as well
        TransportGenerated410 = 20001,
        TimeoutGenerated410 = 20002,
        TransportGenerated503 = 20003,
        Client_CPUOverload = 20004,
        Client_ThreadStarvation = 20005,
        Channel_Closed = 20006,
        MalformedContinuationToken = 20007,
        // EndToEndOperationCancelled = 20008 - cancellation by e2e retry policy - currently only applicable in Java

        //SDK Codes (Server)
        // IMPORTANT - keep these consistent with Java SDK as well
        Server_NameCacheIsStaleExceededRetryLimit = 21001,
        Server_PartitionKeyRangeGoneExceededRetryLimit = 21002,
        Server_CompletingSplitExceededRetryLimit = 21003,
        Server_CompletingPartitionMigrationExceededRetryLimit = 21004,
        ServerGenerated410 = 21005,
        Server_GlobalStrongWriteBarrierNotMet = 21006,
        Server_ReadQuorumNotMet = 21007,
        ServerGenerated503 = 21008,
        Server_NoValidStoreResponse = 21009,
        // ServerGenerated408 = 21010 - currently only applicable in Java

        // Data Transfer Application related
        MissingPartitionKeyInDataTransfer = 22001,
        InvalidPartitionKeyInDataTransfer = 22002
    }

    internal static class StatusCodesExtensions
    {
        private static readonly Dictionary<int, string> CodeNameMap = new Dictionary<int, string>();

        static StatusCodesExtensions()
        {
            StatusCodesExtensions.CodeNameMap[default(int)] = string.Empty;
            foreach (StatusCodes code in Enum.GetValues(typeof(StatusCodes)))
            {
                StatusCodesExtensions.CodeNameMap[(int)code] = code.ToString();
            }
        }

        public static string ToStatusCodeString(this StatusCodes code)
        {
            return StatusCodesExtensions.CodeNameMap.TryGetValue((int)code, out string value) ? value : code.ToString();
        }
    }

    internal static class SubStatusCodesExtensions
    {
        private static readonly Dictionary<int, string> CodeNameMap = new Dictionary<int, string>();
        private static readonly int SDKGeneratedSubStatusStartingCode = 20000;

        static SubStatusCodesExtensions()
        {
            SubStatusCodesExtensions.CodeNameMap[default(int)] = string.Empty;
            foreach (SubStatusCodes code in Enum.GetValues(typeof(SubStatusCodes)))
            {
                SubStatusCodesExtensions.CodeNameMap[(int)code] = code.ToString();
            }
        }

        public static string ToSubStatusCodeString(this SubStatusCodes code)
        {
            return SubStatusCodesExtensions.CodeNameMap.TryGetValue((int)code, out string value) ? value : code.ToString();
        }

        public static bool IsSDKGeneratedSubStatus(this SubStatusCodes code)
        {
            return ((int)code > SubStatusCodesExtensions.SDKGeneratedSubStatusStartingCode);
        }
    }

}
