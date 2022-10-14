//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Concurrent;

    internal static class RntbdConstants
    {
        public const UInt32 CurrentProtocolVersion = 0x00000001;

        public enum RntbdResourceType : ushort
        {
            Connection = 0x0000,
            Database = 0x0001,
            Collection = 0x0002,
            Document = 0x0003,
            Attachment = 0x0004,
            User = 0x0005,
            Permission = 0x0006,
            StoredProcedure = 0x0007,
            Conflict = 0x0008,
            Trigger = 0x0009,
            UserDefinedFunction = 0x000A,
            Module = 0x000B,
            Replica = 0x000C,
            ModuleCommand = 0x000D,
            Record = 0x000E,
            Offer = 0x000F,
            PartitionSetInformation = 0x0010,
            XPReplicatorAddress = 0x0011,
            MasterPartition = 0x0012,
            ServerPartition = 0x0013,
            DatabaseAccount = 0x0014,
            Topology = 0x0015,
            PartitionKeyRange = 0x0016,
            // not used = 0x0017, timestamp
            Schema = 0x0018,
            BatchApply = 0x0019,
            RestoreMetadata = 0x001A,
            ComputeGatewayCharges = 0x001B,
            RidRange = 0x001C,
            UserDefinedType = 0x001D,
            VectorClock = 0x001F,
            PartitionKey = 0x0020,
            Snapshot = 0x0021,
            ClientEncryptionKey = 0x0023,
            Transaction = 0x0025,
            PartitionedSystemDocument = 0x0026,
            RoleDefinition = 0x0027,
            RoleAssignment = 0x0028,
            SystemDocument = 0x0029,
            InteropUser = 0x002A,
            TransportControlCommand = 0x002B,
            AuthPolicyElement = 0x002C,
            StorageAuthToken = 0x002D,
            RetriableWriteCachedResponse = 0x002E,
        }

        public enum RntbdOperationType : ushort
        {
            Connection = 0x0000,
            Create = 0x0001,
            Patch = 0x0002,
            Read = 0x0003,
            ReadFeed = 0x0004,
            Delete = 0x0005,
            Replace = 0x0006,
            // Deprecated and should not be used JPathQuery = 0x0007,
            ExecuteJavaScript = 0x0008,
            SQLQuery = 0x0009,
            Pause = 0x000A,
            Resume = 0x000B,
            Stop = 0x000C,
            Recycle = 0x000D,
            Crash = 0x000E,
            Query = 0x000F,
            ForceConfigRefresh = 0x0010,
            Head = 0x0011,
            HeadFeed = 0x0012,
            Upsert = 0x0013,
            Recreate = 0x0014,
            Throttle = 0x0015,
            GetSplitPoint = 0x0016,
            PreCreateValidation = 0x0017,
            BatchApply = 0x0018,
            AbortSplit = 0x0019,
            CompleteSplit = 0x001A,
            OfferUpdateOperation = 0x001B,
            OfferPreGrowValidation = 0x001C,
            BatchReportThroughputUtilization = 0x001D,
            CompletePartitionMigration = 0x001E,
            AbortPartitionMigration = 0x001F,
            PreReplaceValidation = 0x0020,
            AddComputeGatewayRequestCharges = 0x0021,
            MigratePartition = 0x0022,
            MasterReplaceOfferOperation = 0x023,
            ProvisionedCollectionOfferUpdateOperation = 0x024,
            Batch = 0x025,
            InitiateDatabaseOfferPartitionShrink = 0x026,
            CompleteDatabaseOfferPartitionShrink = 0x027,
            EnsureSnapshotOperation = 0x028,
            GetSplitPoints = 0x0029,
            CompleteMergeOnTarget = 0x002A,
            CompleteMergeOnMaster = 0x002C,
            ForcePartitionBackup = 0x002E,
            CompleteUserTransaction = 0x002F,
            MasterInitiatedProgressCoordination = 0x0030,
            MetadataCheckAccess = 0x0031,
            CreateSystemSnapshot = 0x0032,
            UpdateFailoverPriorityList = 0x0033,
            GetStorageAuthToken = 0x0034,
        }

        public enum ConnectionContextRequestTokenIdentifiers : ushort
        {
            ProtocolVersion = 0x0000,
            ClientVersion = 0x0001,
            UserAgent = 0x0002,
            CallerId = 0x0003,
            EnableChannelMultiplexing = 0x0004,
        }

        public sealed class ConnectionContextRequest : RntbdTokenStream<ConnectionContextRequestTokenIdentifiers>
        {
            public RntbdToken protocolVersion;
            public RntbdToken clientVersion;
            public RntbdToken userAgent;
            public RntbdToken callerId;
            public RntbdToken enableChannelMultiplexing;

            public ConnectionContextRequest()
            {
                this.protocolVersion = new RntbdToken(true, RntbdTokenTypes.ULong, (ushort)ConnectionContextRequestTokenIdentifiers.ProtocolVersion);
                this.clientVersion = new RntbdToken(true, RntbdTokenTypes.SmallString, (ushort)ConnectionContextRequestTokenIdentifiers.ClientVersion);
                this.userAgent = new RntbdToken(true, RntbdTokenTypes.SmallString, (ushort)ConnectionContextRequestTokenIdentifiers.UserAgent);
                this.callerId = new RntbdToken(false, RntbdTokenTypes.Byte, (byte)ConnectionContextRequestTokenIdentifiers.CallerId);
                this.enableChannelMultiplexing = new RntbdToken(false, RntbdTokenTypes.Byte, (byte)ConnectionContextRequestTokenIdentifiers.EnableChannelMultiplexing);

                base.SetTokens(new RntbdToken[]
                {
                    this.protocolVersion,
                    this.clientVersion,
                    this.userAgent,
                    this.callerId,
                    this.enableChannelMultiplexing,
                });
            }
        }

        public enum ConnectionContextResponseTokenIdentifiers : ushort
        {
            ProtocolVersion = 0x0000,
            ClientVersion = 0x0001,
            ServerAgent = 0x0002,
            ServerVersion = 0x0003,
            IdleTimeoutInSeconds = 0x0004,
            UnauthenticatedTimeoutInSeconds = 0x0005,
        }

        public sealed class ConnectionContextResponse : RntbdTokenStream<ConnectionContextResponseTokenIdentifiers>
        {
            public RntbdToken protocolVersion;
            public RntbdToken clientVersion;
            public RntbdToken serverAgent;
            public RntbdToken serverVersion;
            public RntbdToken idleTimeoutInSeconds;
            public RntbdToken unauthenticatedTimeoutInSeconds;

            public ConnectionContextResponse()
            {
                this.protocolVersion = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ConnectionContextResponseTokenIdentifiers.ProtocolVersion);
                this.clientVersion = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)ConnectionContextResponseTokenIdentifiers.ClientVersion);
                this.serverAgent = new RntbdToken(true, RntbdTokenTypes.SmallString, (ushort)ConnectionContextResponseTokenIdentifiers.ServerAgent);
                this.serverVersion = new RntbdToken(true, RntbdTokenTypes.SmallString, (ushort)ConnectionContextResponseTokenIdentifiers.ServerVersion);
                this.idleTimeoutInSeconds = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ConnectionContextResponseTokenIdentifiers.IdleTimeoutInSeconds);
                this.unauthenticatedTimeoutInSeconds = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ConnectionContextResponseTokenIdentifiers.UnauthenticatedTimeoutInSeconds);

                base.SetTokens(new RntbdToken[]
                {
                    this.protocolVersion,
                    this.clientVersion,
                    this.serverAgent,
                    this.serverVersion,
                    this.idleTimeoutInSeconds,
                    this.unauthenticatedTimeoutInSeconds,
                });
            }
        }

        public enum RntbdIndexingDirective : byte
        {
            Default = 0x00,
            Include = 0x01,
            Exclude = 0x02,

            Invalid = 0xFF,
        }

        public enum RntbdMigrateCollectionDirective : byte
        {
            Thaw = 0x00,
            Freeze = 0x01,

            Invalid = 0xFF,
        }

        public enum RntbdRemoteStorageType : byte
        {
            Invalid = 0x00,
            NotSpecified = 0x01,
            Standard = 0x02,
            Premium = 0x03,
        }

        public enum RntbdConsistencyLevel : byte
        {
            Strong = 0x00,
            BoundedStaleness = 0x01,
            Session = 0x02,
            Eventual = 0x03,
            ConsistentPrefix = 0x04,

            Invalid = 0xFF,
        }

        public enum RntdbEnumerationDirection : byte
        {
            Invalid = 0x00,
            Forward = 0x01,
            Reverse = 0x02,
        }

        public enum RntbdFanoutOperationState : byte
        {
            Started = 0x01,
            Completed = 0x02,
        }

        public enum RntdbReadFeedKeyType : byte
        {
            Invalid = 0x00,
            ResourceId = 0x01,
            EffectivePartitionKey = 0x02,
            EffectivePartitionKeyRange = 0x03,
        }

        public enum RntbdContentSerializationFormat : byte
        {
            JsonText = 0x00,
            CosmosBinary = 0x01,
            HybridRow = 0x02,

            Invalid = 0xFF,
        }

        public enum RntbdSystemDocumentType : byte
        {
            PartitionKey = 0x00,
            MaterializedViewLeaseDocument = 0x01,
            MaterializedViewBuilderOwnershipDocument = 0x02,
            MaterializedViewLeaseStoreInitDocument = 0x03,

            Invalid = 0xFF,
        }

        public enum RntbdRequestedCollectionType : byte
        {
            All = 0x00,
            Standard = 0x01,
            MaterializedView = 0x02,
        }

        public enum RequestIdentifiers : ushort
        {
            ResourceId = 0x0000,
            AuthorizationToken = 0x0001,
            PayloadPresent = 0x0002,
            Date = 0x0003,
            PageSize = 0x0004,
            SessionToken = 0x0005,
            ContinuationToken = 0x0006,
            IndexingDirective = 0x0007,
            Match = 0x0008,
            PreTriggerInclude = 0x0009,
            PostTriggerInclude = 0x000A,
            IsFanout = 0x000B,
            CollectionPartitionIndex = 0x000C,
            CollectionServiceIndex = 0x000D,
            PreTriggerExclude = 0x000E,
            PostTriggerExclude = 0x000F,
            ConsistencyLevel = 0x0010,
            EntityId = 0x0011,
            ResourceSchemaName = 0x0012,
            ReplicaPath = 0x0013,
            ResourceTokenExpiry = 0x0014,
            DatabaseName = 0x0015,
            CollectionName = 0x0016,
            DocumentName = 0x0017,
            AttachmentName = 0x0018,
            UserName = 0x0019,
            PermissionName = 0x001A,
            StoredProcedureName = 0x001B,
            UserDefinedFunctionName = 0x001C,
            TriggerName = 0x001D,
            EnableScanInQuery = 0x001E,
            EmitVerboseTracesInQuery = 0x001F,
            ConflictName = 0x0020,
            BindReplicaDirective = 0x0021,
            PrimaryMasterKey = 0x0022,
            SecondaryMasterKey = 0x0023,
            PrimaryReadonlyKey = 0x0024,
            SecondaryReadonlyKey = 0x0025,
            ProfileRequest = 0x0026,
            EnableLowPrecisionOrderBy = 0x0027,
            ClientVersion = 0x0028,
            CanCharge = 0x0029,
            CanThrottle = 0x002A,
            PartitionKey = 0x002B,
            PartitionKeyRangeId = 0x002C,
            NotUsed2D = 0x002D,
            NotUsed2E = 0x002E,
            NotUsed2F = 0x002F,
            // not used 0x0030,
            MigrateCollectionDirective = 0x0031,
            NotUsed32 = 0x0032,
            SupportSpatialLegacyCoordinates = 0x0033,
            PartitionCount = 0x0034,
            CollectionRid = 0x0035,
            PartitionKeyRangeName = 0x0036,
            // not used = 0x0037, RoundTripTimeInMsec
            // not used = 0x0038, RequestMessageSentTime
            // not used = 0x0039, RequestMessageTimeOffset
            SchemaName = 0x003A,
            FilterBySchemaRid = 0x003B,
            UsePolygonsSmallerThanAHemisphere = 0x003C,
            GatewaySignature = 0x003D,
            EnableLogging = 0x003E,
            A_IM = 0x003F,
            PopulateQuotaInfo = 0x0040,
            DisableRUPerMinuteUsage = 0x0041,
            PopulateQueryMetrics = 0x0042,
            ResponseContinuationTokenLimitInKb = 0x0043,
            PopulatePartitionStatistics = 0x0044,
            RemoteStorageType = 0x0045,
            CollectionRemoteStorageSecurityIdentifier = 0x0046,
            IfModifiedSince = 0x0047,
            PopulateCollectionThroughputInfo = 0x0048,
            RemainingTimeInMsOnClientRequest = 0x0049,
            ClientRetryAttemptCount = 0x004A,
            TargetLsn = 0x004B,
            TargetGlobalCommittedLsn = 0x004C,
            TransportRequestID = 0x004D,
            RestoreMetadaFilter = 0x004E,
            RestoreParams = 0x004F,
            ShareThroughput = 0x0050,
            PartitionResourceFilter = 0x0051,
            IsReadOnlyScript = 0x0052,
            IsAutoScaleRequest = 0x0053,
            ForceQueryScan = 0x0054,
            // not used = 0x0055, LeaseSeqNumber
            CanOfferReplaceComplete = 0x0056,
            ExcludeSystemProperties = 0x0057,
            BinaryId = 0x0058,
            TimeToLiveInSeconds = 0x0059,
            EffectivePartitionKey = 0x005A,
            BinaryPassthroughRequest = 0x005B,
            UserDefinedTypeName = 0x005C,
            EnableDynamicRidRangeAllocation = 0x005D,
            EnumerationDirection = 0x005E,
            StartId = 0x005F,
            EndId = 0x0060,
            FanoutOperationState = 0x0061,
            StartEpk = 0x0062,
            EndEpk = 0x0063,
            ReadFeedKeyType = 0x0064,
            ContentSerializationFormat = 0x0065,
            AllowTentativeWrites = 0x0066,
            IsUserRequest = 0x0067,
            SharedOfferthroughput = 0x0068,   // Not used
            PreserveFullContent = 0x0069,
            IncludeTentativeWrites = 0x0070,
            PopulateResourceCount = 0x0071,
            MergeStaticId = 0x0072,
            IsBatchAtomic = 0x0073,
            ShouldBatchContinueOnError = 0x0074,
            IsBatchOrdered = 0x0075,
            SchemaOwnerRid = 0x0076,
            SchemaHash = 0x0077,
            IsRUPerGBEnforcementRequest = 0x0078,
            MaxPollingIntervalMilliseconds = 0x0079,
            SnapshotName = 0x007A,
            PopulateLogStoreInfo = 0x007B,
            GetAllPartitionKeyStatistics = 0x007C,
            ForceSideBySideIndexMigration = 0x007D,
            CollectionChildResourceNameLimitInBytes = 0x007E,
            CollectionChildResourceContentLengthLimitInKB = 0x007F,
            ClientEncryptionKeyName = 0x0080,
            MergeCheckpointGLSNKeyName = 0x0081,
            ReturnPreference = 0x0082,
            UniqueIndexNameEncodingMode = 0x0083,
            PopulateUnflushedMergeEntryCount = 0x0084,
            MigrateOfferToManualThroughput = 0x0085,
            MigrateOfferToAutopilot = 0x0086,
            IsClientEncrypted = 0x0087,
            SystemDocumentType = 0x0088,
            IsofferStorageRefreshRequest = 0x0089,
            ResourceTypes = 0x008A,
            TransactionId = 0x008B,
            TransactionFirstRequest = 0x008C,
            TransactionCommit = 0x008D,
            SystemDocumentName = 0x008E,
            UpdateMaxThroughputEverProvisioned = 0x008F,
            UniqueIndexReIndexingState = 0x0090,
            RoleDefinitionName = 0x0091,
            RoleAssignmentName = 0x0092,
            UseSystemBudget = 0x0093,
            IgnoreSystemLoweringMaxThroughput = 0x0094,
            TruncateMergeLogRequest = 0x0095,
            RetriableWriteRequestId = 0x0096,
            IsRetriedWriteReqeuest = 0x0097,
            RetriableWriteRequestStartTimestamp = 0x0098,
            AddResourcePropertiesToResponse = 0x0099,
            ChangeFeedStartFullFidelityIfNoneMatch = 0x009A,
            SystemRestoreOperation = 0x009B,
            SkipRefreshDatabaseAccountConfigs = 0x009C,
            IntendedCollectionRid = 0x009D,
            UseArchivalPartition = 0x009E,
            PopulateUniqueIndexReIndexProgress = 0x009F,
            CollectionSchemaId = 0x00A0,
            CollectionTruncate = 0x00A1,
            SDKSupportedCapabilities = 0x00A2,
            IsMaterializedViewBuild = 0x00A3,
            BuilderClientIdentifier = 0x00A4,
            SourceCollectionIfMatch = 0x00A5,
            RequestedCollectionType = 0x00A6,
            // 0x00A7 is unused,
            InteropUserName = 0x00A8,
            PopulateIndexMetrics = 0x00A9,
            PopulateAnalyticalMigrationProgress = 0x00AA,
            AuthPolicyElementName = 0x00AB,
            ShouldReturnCurrentServerDateTime = 0x00AC,
            RbacUserId = 0x00AD,
            RbacAction = 0x00AE,
            RbacResource = 0x00AF,
            CorrelatedActivityId = 0x00B0,
            IsThroughputCapRequest = 0x00B1,
            ChangeFeedWireFormatVersion = 0x00B2,
            PopulateBYOKEncryptionProgress = 0x00B3,
            UseUserBackgroundBudget = 0x00B4,
            IncludePhysicalPartitionThroughputInfo = 0x00B5,
            IsServerlessStorageRefreshRequest = 0x00B6,
            UpdateOfferStateToPending = 0x00B7,
            PopulateOldestActiveSchema = 0x00B8,
            IsInternalServerlessRequest = 0x00B9,
            OfferReplaceRURedistribution = 0x00BA,
        }

        public sealed class Request : RntbdTokenStream<RequestIdentifiers>
        {
            public RntbdToken resourceId;
            public RntbdToken authorizationToken;
            public RntbdToken payloadPresent;
            public RntbdToken date;
            public RntbdToken pageSize;
            public RntbdToken sessionToken;
            public RntbdToken continuationToken;
            public RntbdToken indexingDirective;
            public RntbdToken match;
            public RntbdToken preTriggerInclude;
            public RntbdToken postTriggerInclude;
            public RntbdToken isFanout;
            public RntbdToken collectionPartitionIndex;
            public RntbdToken collectionServiceIndex;
            public RntbdToken preTriggerExclude;
            public RntbdToken postTriggerExclude;
            public RntbdToken consistencyLevel;
            public RntbdToken entityId;
            public RntbdToken resourceSchemaName;
            public RntbdToken replicaPath;
            public RntbdToken resourceTokenExpiry;
            public RntbdToken databaseName;
            public RntbdToken collectionName;
            public RntbdToken documentName;
            public RntbdToken attachmentName;
            public RntbdToken userName;
            public RntbdToken permissionName;
            public RntbdToken storedProcedureName;
            public RntbdToken userDefinedFunctionName;
            public RntbdToken triggerName;
            public RntbdToken enableScanInQuery;
            public RntbdToken emitVerboseTracesInQuery;
            public RntbdToken conflictName;
            public RntbdToken bindReplicaDirective;
            public RntbdToken primaryMasterKey;
            public RntbdToken secondaryMasterKey;
            public RntbdToken primaryReadonlyKey;
            public RntbdToken secondaryReadonlyKey;
            public RntbdToken profileRequest;
            public RntbdToken enableLowPrecisionOrderBy;
            public RntbdToken clientVersion;
            public RntbdToken canCharge;
            public RntbdToken canThrottle;
            public RntbdToken partitionKey;
            public RntbdToken partitionKeyRangeId;
            public RntbdToken migrateCollectionDirective;
            public RntbdToken supportSpatialLegacyCoordinates;
            public RntbdToken partitionCount;
            public RntbdToken collectionRid;
            public RntbdToken partitionKeyRangeName;
            public RntbdToken schemaName;
            public RntbdToken filterBySchemaRid;
            public RntbdToken usePolygonsSmallerThanAHemisphere;
            public RntbdToken gatewaySignature;
            public RntbdToken enableLogging;
            public RntbdToken a_IM;
            public RntbdToken ifModifiedSince;
            public RntbdToken populateQuotaInfo;
            public RntbdToken disableRUPerMinuteUsage;
            public RntbdToken populateQueryMetrics;
            public RntbdToken responseContinuationTokenLimitInKb;
            public RntbdToken populatePartitionStatistics;
            public RntbdToken remoteStorageType;
            public RntbdToken remainingTimeInMsOnClientRequest;
            public RntbdToken clientRetryAttemptCount;
            public RntbdToken targetLsn;
            public RntbdToken targetGlobalCommittedLsn;
            public RntbdToken transportRequestID;
            public RntbdToken collectionRemoteStorageSecurityIdentifier;
            public RntbdToken populateCollectionThroughputInfo;
            public RntbdToken restoreMetadataFilter;
            public RntbdToken restoreParams;
            public RntbdToken shareThroughput;
            public RntbdToken partitionResourceFilter;
            public RntbdToken isReadOnlyScript;
            public RntbdToken isAutoScaleRequest;
            public RntbdToken forceQueryScan;
            public RntbdToken canOfferReplaceComplete;
            public RntbdToken excludeSystemProperties;
            public RntbdToken binaryId;
            public RntbdToken timeToLiveInSeconds;
            public RntbdToken effectivePartitionKey;
            public RntbdToken binaryPassthroughRequest;
            public RntbdToken userDefinedTypeName;
            public RntbdToken enableDynamicRidRangeAllocation;
            public RntbdToken enumerationDirection;
            public RntbdToken StartId;
            public RntbdToken EndId;
            public RntbdToken FanoutOperationState;
            public RntbdToken StartEpk;
            public RntbdToken EndEpk;
            public RntbdToken readFeedKeyType;
            public RntbdToken contentSerializationFormat;
            public RntbdToken allowTentativeWrites;
            public RntbdToken isUserRequest;
            public RntbdToken preserveFullContent;
            public RntbdToken includeTentativeWrites;
            public RntbdToken populateResourceCount;
            public RntbdToken mergeStaticId;
            public RntbdToken isBatchAtomic;
            public RntbdToken shouldBatchContinueOnError;
            public RntbdToken isBatchOrdered;
            public RntbdToken schemaOwnerRid;
            public RntbdToken schemaHash;
            public RntbdToken isRUPerGBEnforcementRequest;
            public RntbdToken maxPollingIntervalMilliseconds;
            public RntbdToken snapshotName;
            public RntbdToken populateLogStoreInfo;
            public RntbdToken getAllPartitionKeyStatistics;
            public RntbdToken forceSideBySideIndexMigration;
            public RntbdToken collectionChildResourceNameLimitInBytes;
            public RntbdToken collectionChildResourceContentLengthLimitInKB;
            public RntbdToken clientEncryptionKeyName;
            public RntbdToken mergeCheckpointGlsnKeyName;
            public RntbdToken returnPreference;
            public RntbdToken uniqueIndexNameEncodingMode;
            public RntbdToken populateUnflushedMergeEntryCount;
            public RntbdToken migrateOfferToManualThroughput;
            public RntbdToken migrateOfferToAutopilot;
            public RntbdToken isClientEncrypted;
            public RntbdToken systemDocumentType;
            public RntbdToken isofferStorageRefreshRequest;
            public RntbdToken resourceTypes;
            public RntbdToken transactionId;
            public RntbdToken transactionFirstRequest;
            public RntbdToken transactionCommit;
            public RntbdToken systemDocumentName;
            public RntbdToken updateMaxThroughputEverProvisioned;
            public RntbdToken uniqueIndexReIndexingState;
            public RntbdToken roleDefinitionName;
            public RntbdToken roleAssignmentName;
            public RntbdToken useSystemBudget;
            public RntbdToken ignoreSystemLoweringMaxThroughput;
            public RntbdToken truncateMergeLogRequest;
            public RntbdToken retriableWriteRequestId;
            public RntbdToken isRetriedWriteRequest;
            public RntbdToken retriableWriteRequestStartTimestamp;
            public RntbdToken addResourcePropertiesToResponse;
            public RntbdToken changeFeedStartFullFidelityIfNoneMatch;
            public RntbdToken systemRestoreOperation;
            public RntbdToken skipRefreshDatabaseAccountConfigs;
            public RntbdToken intendedCollectionRid;
            public RntbdToken useArchivalPartition;
            public RntbdToken populateUniqueIndexReIndexProgress;
            public RntbdToken schemaId;
            public RntbdToken collectionTruncate;
            public RntbdToken sdkSupportedCapabilities;
            public RntbdToken isMaterializedViewBuild;
            public RntbdToken builderClientIdentifier;
            public RntbdToken sourceCollectionIfMatch;
            public RntbdToken requestedCollectionType;
            public RntbdToken interopUserName;
            public RntbdToken populateIndexMetrics;
            public RntbdToken populateAnalyticalMigrationProgress;
            public RntbdToken authPolicyElementName;
            public RntbdToken shouldReturnCurrentServerDateTime;
            public RntbdToken rbacUserId;
            public RntbdToken rbacAction;
            public RntbdToken rbacResource;
            public RntbdToken correlatedActivityId;
            public RntbdToken isThroughputCapRequest;
            public RntbdToken changeFeedWireFormatVersion;
            public RntbdToken populateBYOKEncryptionProgress;
            public RntbdToken useUserBackgroundBudget;
            public RntbdToken includePhysicalPartitionThroughputInfo;
            public RntbdToken isServerlessStorageRefreshRequest;
            public RntbdToken updateOfferStateToPending;
            public RntbdToken populateOldestActiveSchema;
            public RntbdToken isInternalServerlessRequest;
            public RntbdToken offerReplaceRURedistribution;

            public Request()
            {
                this.resourceId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.ResourceId);
                this.authorizationToken = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.AuthorizationToken);
                this.payloadPresent = new RntbdToken(true, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PayloadPresent);
                this.date = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)RequestIdentifiers.Date);
                this.pageSize = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.PageSize);
                this.sessionToken = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SessionToken);
                this.continuationToken = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ContinuationToken);
                this.indexingDirective = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IndexingDirective);
                this.match = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.Match);
                this.preTriggerInclude = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PreTriggerInclude);
                this.postTriggerInclude = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PostTriggerInclude);
                this.isFanout = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsFanout);
                this.collectionPartitionIndex = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.CollectionPartitionIndex);
                this.collectionServiceIndex = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.CollectionServiceIndex);
                this.preTriggerExclude = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PreTriggerExclude);
                this.postTriggerExclude = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PostTriggerExclude);
                this.consistencyLevel = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ConsistencyLevel);
                this.entityId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.EntityId);
                this.resourceSchemaName = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)RequestIdentifiers.ResourceSchemaName);
                this.replicaPath = new RntbdToken(true, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ReplicaPath);
                this.resourceTokenExpiry = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.ResourceTokenExpiry);
                this.databaseName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.DatabaseName);
                this.collectionName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.CollectionName);
                this.documentName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.DocumentName);
                this.attachmentName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.AttachmentName);
                this.userName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.UserName);
                this.permissionName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PermissionName);
                this.storedProcedureName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.StoredProcedureName);
                this.userDefinedFunctionName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.UserDefinedFunctionName);
                this.triggerName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.TriggerName);
                this.enableScanInQuery = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EnableScanInQuery);
                this.emitVerboseTracesInQuery = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EmitVerboseTracesInQuery);
                this.conflictName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ConflictName);
                this.bindReplicaDirective = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.BindReplicaDirective);
                this.primaryMasterKey = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PrimaryMasterKey);
                this.secondaryMasterKey = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SecondaryMasterKey);
                this.primaryReadonlyKey = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PrimaryReadonlyKey);
                this.secondaryReadonlyKey = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SecondaryReadonlyKey);
                this.profileRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ProfileRequest);
                this.enableLowPrecisionOrderBy = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EnableLowPrecisionOrderBy);
                this.clientVersion = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)RequestIdentifiers.ClientVersion);
                this.canCharge = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.CanCharge);
                this.canThrottle = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.CanThrottle);
                this.partitionKey = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PartitionKey);
                this.partitionKeyRangeId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PartitionKeyRangeId);
                this.migrateCollectionDirective = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.MigrateCollectionDirective);
                this.supportSpatialLegacyCoordinates = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.SupportSpatialLegacyCoordinates);
                this.partitionCount = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.PartitionCount);
                this.collectionRid = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.CollectionRid);
                this.partitionKeyRangeName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PartitionKeyRangeName);
                this.schemaName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SchemaName);
                this.filterBySchemaRid = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.FilterBySchemaRid);
                this.usePolygonsSmallerThanAHemisphere = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UsePolygonsSmallerThanAHemisphere);
                this.gatewaySignature = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.GatewaySignature);
                this.enableLogging = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EnableLogging);
                this.a_IM = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.A_IM);
                this.ifModifiedSince = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.IfModifiedSince);
                this.populateQuotaInfo = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateQuotaInfo);
                this.disableRUPerMinuteUsage = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.DisableRUPerMinuteUsage);
                this.populateQueryMetrics = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateQueryMetrics);
                this.responseContinuationTokenLimitInKb = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.ResponseContinuationTokenLimitInKb);
                this.populatePartitionStatistics = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulatePartitionStatistics);
                this.remoteStorageType = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.RemoteStorageType);
                this.collectionRemoteStorageSecurityIdentifier = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.CollectionRemoteStorageSecurityIdentifier);
                this.populateCollectionThroughputInfo = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateCollectionThroughputInfo);
                this.remainingTimeInMsOnClientRequest = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.RemainingTimeInMsOnClientRequest);
                this.clientRetryAttemptCount = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.ClientRetryAttemptCount);
                this.targetLsn = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)RequestIdentifiers.TargetLsn);
                this.targetGlobalCommittedLsn = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)RequestIdentifiers.TargetGlobalCommittedLsn);
                this.transportRequestID = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.TransportRequestID);
                this.restoreMetadataFilter = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RestoreMetadaFilter);
                this.restoreParams = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RestoreParams);
                this.shareThroughput = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ShareThroughput);
                this.partitionResourceFilter = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.PartitionResourceFilter);
                this.isReadOnlyScript = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsReadOnlyScript);
                this.isAutoScaleRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsAutoScaleRequest);
                this.forceQueryScan = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ForceQueryScan);
                this.canOfferReplaceComplete = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.CanOfferReplaceComplete);
                this.excludeSystemProperties = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ExcludeSystemProperties);
                this.binaryId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.BinaryId);
                this.timeToLiveInSeconds = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.TimeToLiveInSeconds);
                this.effectivePartitionKey = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.EffectivePartitionKey);
                this.binaryPassthroughRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.BinaryPassthroughRequest);
                this.userDefinedTypeName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.UserDefinedTypeName);
                this.enableDynamicRidRangeAllocation = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EnableDynamicRidRangeAllocation);
                this.enumerationDirection = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.EnumerationDirection);
                this.StartId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.StartId);
                this.EndId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.EndId);
                this.FanoutOperationState = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.FanoutOperationState);
                this.StartEpk = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.StartEpk);
                this.EndEpk = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.EndEpk);
                this.readFeedKeyType = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ReadFeedKeyType);
                this.contentSerializationFormat = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ContentSerializationFormat);
                this.allowTentativeWrites = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.AllowTentativeWrites);
                this.isUserRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsUserRequest);
                this.preserveFullContent = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PreserveFullContent);
                this.includeTentativeWrites = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IncludeTentativeWrites);
                this.populateResourceCount = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateResourceCount);
                this.mergeStaticId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.MergeStaticId);
                this.isBatchAtomic = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsBatchAtomic);
                this.shouldBatchContinueOnError = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ShouldBatchContinueOnError);
                this.isBatchOrdered = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsBatchOrdered);
                this.schemaOwnerRid = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SchemaOwnerRid);
                this.schemaHash = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.SchemaHash);
                this.isRUPerGBEnforcementRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsRUPerGBEnforcementRequest);
                this.maxPollingIntervalMilliseconds = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.MaxPollingIntervalMilliseconds);
                this.snapshotName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SnapshotName);
                this.populateLogStoreInfo = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateLogStoreInfo);
                this.getAllPartitionKeyStatistics = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.GetAllPartitionKeyStatistics);
                this.forceSideBySideIndexMigration = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ForceSideBySideIndexMigration);
                this.collectionChildResourceNameLimitInBytes = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.CollectionChildResourceNameLimitInBytes);
                this.collectionChildResourceContentLengthLimitInKB = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.CollectionChildResourceContentLengthLimitInKB);
                this.clientEncryptionKeyName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ClientEncryptionKeyName);
                this.mergeCheckpointGlsnKeyName = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)RequestIdentifiers.MergeCheckpointGLSNKeyName);
                this.returnPreference = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ReturnPreference);
                this.uniqueIndexNameEncodingMode = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UniqueIndexNameEncodingMode);
                this.populateUnflushedMergeEntryCount = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateUnflushedMergeEntryCount);
                this.migrateOfferToManualThroughput = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.MigrateOfferToManualThroughput);
                this.migrateOfferToAutopilot = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.MigrateOfferToAutopilot);
                this.isClientEncrypted = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsClientEncrypted);
                this.systemDocumentType = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.SystemDocumentType);
                this.isofferStorageRefreshRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsofferStorageRefreshRequest);
                this.resourceTypes = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ResourceTypes);
                this.transactionId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.TransactionId);
                this.transactionFirstRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.TransactionFirstRequest);
                this.transactionCommit = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.TransactionCommit);
                this.systemDocumentName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SystemDocumentName);
                this.updateMaxThroughputEverProvisioned = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.UpdateMaxThroughputEverProvisioned);
                this.uniqueIndexReIndexingState = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UniqueIndexReIndexingState);
                this.roleDefinitionName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RoleDefinitionName);
                this.roleAssignmentName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RoleAssignmentName);
                this.useSystemBudget = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UseSystemBudget);
                this.ignoreSystemLoweringMaxThroughput = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IgnoreSystemLoweringMaxThroughput);
                this.truncateMergeLogRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.TruncateMergeLogRequest);
                this.retriableWriteRequestId = new RntbdToken(false, RntbdTokenTypes.Bytes, (ushort)RequestIdentifiers.RetriableWriteRequestId);
                this.isRetriedWriteRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsRetriedWriteReqeuest);
                this.retriableWriteRequestStartTimestamp = new RntbdToken(false, RntbdTokenTypes.ULongLong, (ushort)RequestIdentifiers.RetriableWriteRequestStartTimestamp);
                this.addResourcePropertiesToResponse = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.AddResourcePropertiesToResponse);
                this.changeFeedStartFullFidelityIfNoneMatch = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ChangeFeedStartFullFidelityIfNoneMatch);
                this.systemRestoreOperation = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.SystemRestoreOperation);
                this.skipRefreshDatabaseAccountConfigs = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.SkipRefreshDatabaseAccountConfigs);
                this.intendedCollectionRid = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.IntendedCollectionRid);
                this.useArchivalPartition = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UseArchivalPartition);
                this.populateUniqueIndexReIndexProgress = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateUniqueIndexReIndexProgress);
                this.schemaId = new RntbdToken(false, RntbdTokenTypes.Long, (ushort)RequestIdentifiers.CollectionSchemaId);
                this.collectionTruncate = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.CollectionTruncate);
                this.sdkSupportedCapabilities = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)RequestIdentifiers.SDKSupportedCapabilities);
                this.isMaterializedViewBuild = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsMaterializedViewBuild);
                this.builderClientIdentifier = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.BuilderClientIdentifier);
                this.sourceCollectionIfMatch = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.SourceCollectionIfMatch);
                this.requestedCollectionType = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.RequestedCollectionType);
                this.interopUserName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.InteropUserName);
                this.populateIndexMetrics = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateIndexMetrics);
                this.populateAnalyticalMigrationProgress = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateAnalyticalMigrationProgress);
                this.authPolicyElementName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.AuthPolicyElementName);
                this.shouldReturnCurrentServerDateTime = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.ShouldReturnCurrentServerDateTime);
                this.rbacUserId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RbacUserId);
                this.rbacAction = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RbacAction);
                this.rbacResource = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.RbacResource);
                this.correlatedActivityId = new RntbdToken(false, RntbdTokenTypes.Guid, (ushort)RequestIdentifiers.CorrelatedActivityId);
                this.isThroughputCapRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsThroughputCapRequest);
                this.changeFeedWireFormatVersion = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.ChangeFeedWireFormatVersion);
                this.populateBYOKEncryptionProgress = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateBYOKEncryptionProgress);
                this.useUserBackgroundBudget = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UseUserBackgroundBudget);
                this.includePhysicalPartitionThroughputInfo = new RntbdToken(false, RntbdTokenTypes.String, (ushort)RequestIdentifiers.IncludePhysicalPartitionThroughputInfo);
                this.isServerlessStorageRefreshRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsServerlessStorageRefreshRequest);
                this.updateOfferStateToPending = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.UpdateOfferStateToPending);
                this.populateOldestActiveSchema = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.PopulateOldestActiveSchema);
                this.isInternalServerlessRequest = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.IsInternalServerlessRequest);
                this.offerReplaceRURedistribution = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)RequestIdentifiers.OfferReplaceRURedistribution);

                base.SetTokens(new RntbdToken[]
                {
                    this.resourceId,
                    this.authorizationToken,
                    this.payloadPresent,
                    this.date,
                    this.pageSize,
                    this.sessionToken,
                    this.continuationToken,
                    this.indexingDirective,
                    this.match,
                    this.preTriggerInclude,
                    this.postTriggerInclude,
                    this.isFanout,
                    this.collectionPartitionIndex,
                    this.collectionServiceIndex,
                    this.preTriggerExclude,
                    this.postTriggerExclude,
                    this.consistencyLevel,
                    this.entityId,
                    this.resourceSchemaName,
                    this.replicaPath,
                    this.resourceTokenExpiry,
                    this.databaseName,
                    this.collectionName,
                    this.documentName,
                    this.attachmentName,
                    this.userName,
                    this.permissionName,
                    this.storedProcedureName,
                    this.userDefinedFunctionName,
                    this.triggerName,
                    this.enableScanInQuery,
                    this.emitVerboseTracesInQuery,
                    this.conflictName,
                    this.bindReplicaDirective,
                    this.primaryMasterKey,
                    this.secondaryMasterKey,
                    this.primaryReadonlyKey,
                    this.secondaryReadonlyKey,
                    this.profileRequest,
                    this.enableLowPrecisionOrderBy,
                    this.clientVersion,
                    this.canCharge,
                    this.canThrottle,
                    this.partitionKey,
                    this.partitionKeyRangeId,
                    this.migrateCollectionDirective,
                    this.supportSpatialLegacyCoordinates,
                    this.partitionCount,
                    this.collectionRid,
                    this.partitionKeyRangeName,
                    this.schemaName,
                    this.filterBySchemaRid,
                    this.usePolygonsSmallerThanAHemisphere,
                    this.gatewaySignature,
                    this.enableLogging,
                    this.a_IM,
                    this.ifModifiedSince,
                    this.populateQuotaInfo,
                    this.disableRUPerMinuteUsage,
                    this.populateQueryMetrics,
                    this.responseContinuationTokenLimitInKb,
                    this.populatePartitionStatistics,
                    this.remoteStorageType,
                    this.collectionRemoteStorageSecurityIdentifier,
                    this.populateCollectionThroughputInfo,
                    this.remainingTimeInMsOnClientRequest,
                    this.clientRetryAttemptCount,
                    this.targetLsn,
                    this.targetGlobalCommittedLsn,
                    this.transportRequestID,
                    this.restoreMetadataFilter,
                    this.restoreParams,
                    this.shareThroughput,
                    this.partitionResourceFilter,
                    this.isReadOnlyScript,
                    this.isAutoScaleRequest,
                    this.forceQueryScan,
                    this.canOfferReplaceComplete,
                    this.excludeSystemProperties,
                    this.binaryId,
                    this.timeToLiveInSeconds,
                    this.effectivePartitionKey,
                    this.binaryPassthroughRequest,
                    this.userDefinedTypeName,
                    this.enableDynamicRidRangeAllocation,
                    this.enumerationDirection,
                    this.StartId,
                    this.EndId,
                    this.FanoutOperationState,
                    this.StartEpk,
                    this.EndEpk,
                    this.readFeedKeyType,
                    this.contentSerializationFormat,
                    this.allowTentativeWrites,
                    this.isUserRequest,
                    this.preserveFullContent,
                    this.includeTentativeWrites,
                    this.populateResourceCount,
                    this.mergeStaticId,
                    this.isBatchAtomic,
                    this.shouldBatchContinueOnError,
                    this.isBatchOrdered,
                    this.schemaOwnerRid,
                    this.schemaHash,
                    this.isRUPerGBEnforcementRequest,
                    this.maxPollingIntervalMilliseconds,
                    this.snapshotName,
                    this.populateLogStoreInfo,
                    this.getAllPartitionKeyStatistics,
                    this.forceSideBySideIndexMigration,
                    this.collectionChildResourceNameLimitInBytes,
                    this.collectionChildResourceContentLengthLimitInKB,
                    this.clientEncryptionKeyName,
                    this.mergeCheckpointGlsnKeyName,
                    this.returnPreference,
                    this.uniqueIndexNameEncodingMode,
                    this.populateUnflushedMergeEntryCount,
                    this.migrateOfferToManualThroughput,
                    this.migrateOfferToAutopilot,
                    this.isClientEncrypted,
                    this.systemDocumentType,
                    this.isofferStorageRefreshRequest,
                    this.resourceTypes,
                    this.transactionId,
                    this.transactionFirstRequest,
                    this.transactionCommit,
                    this.systemDocumentName,
                    this.updateMaxThroughputEverProvisioned,
                    this.uniqueIndexReIndexingState,
                    this.roleDefinitionName,
                    this.roleAssignmentName,
                    this.useSystemBudget,
                    this.ignoreSystemLoweringMaxThroughput,
                    this.truncateMergeLogRequest,
                    this.retriableWriteRequestId,
                    this.isRetriedWriteRequest,
                    this.retriableWriteRequestStartTimestamp,
                    this.addResourcePropertiesToResponse,
                    this.changeFeedStartFullFidelityIfNoneMatch,
                    this.systemRestoreOperation,
                    this.skipRefreshDatabaseAccountConfigs,
                    this.intendedCollectionRid,
                    this.useArchivalPartition,
                    this.populateUniqueIndexReIndexProgress,
                    this.schemaId,
                    this.collectionTruncate,
                    this.sdkSupportedCapabilities,
                    this.isMaterializedViewBuild,
                    this.builderClientIdentifier,
                    this.sourceCollectionIfMatch,
                    this.requestedCollectionType,
                    this.interopUserName,
                    this.populateIndexMetrics,
                    this.populateAnalyticalMigrationProgress,
                    this.authPolicyElementName,
                    this.shouldReturnCurrentServerDateTime,
                    this.rbacUserId,
                    this.rbacAction,
                    this.rbacResource,
                    this.correlatedActivityId,
                    this.isThroughputCapRequest,
                    this.changeFeedWireFormatVersion,
                    this.populateBYOKEncryptionProgress,
                    this.useUserBackgroundBudget,
                    this.includePhysicalPartitionThroughputInfo,
                    this.isServerlessStorageRefreshRequest,
                    this.updateOfferStateToPending,
                    this.populateOldestActiveSchema,
                    this.isInternalServerlessRequest,
                    this.offerReplaceRURedistribution
                });
            }
        }

        public enum ResponseIdentifiers : ushort
        {
            PayloadPresent = 0x0000,
            // not used = 0x0001,
            LastStateChangeDateTime = 0x0002,
            ContinuationToken = 0x0003,
            ETag = 0x0004,
            // not used = 0x005,
            // not used = 0x006,
            ReadsPerformed = 0x0007,
            WritesPerformed = 0x0008,
            QueriesPerformed = 0x0009,
            IndexTermsGenerated = 0x000A,
            ScriptsExecuted = 0x000B,
            RetryAfterMilliseconds = 0x000C,
            IndexingDirective = 0x000D,
            StorageMaxResoureQuota = 0x000E,
            StorageResourceQuotaUsage = 0x000F,
            SchemaVersion = 0x0010,
            CollectionPartitionIndex = 0x0011,
            CollectionServiceIndex = 0x0012,
            LSN = 0x0013,
            ItemCount = 0x0014,
            RequestCharge = 0x0015,
            // not used = 0x0016,
            OwnerFullName = 0x0017,
            OwnerId = 0x0018,
            DatabaseAccountId = 0x0019,
            QuorumAckedLSN = 0x001A,
            RequestValidationFailure = 0x001B,
            SubStatus = 0x001C,
            CollectionUpdateProgress = 0x001D,
            CurrentWriteQuorum = 0x001E,
            CurrentReplicaSetSize = 0x001F,
            CollectionLazyIndexProgress = 0x0020,
            PartitionKeyRangeId = 0x0021,
            // not used = 0x0022, RequestMessageReceivedTime
            // not used = 0x0023, ResponseMessageSentTime
            // not used = 0x0024, ResponseMessageTimeOffset
            LogResults = 0x0025,
            XPRole = 0x0026,
            IsRUPerMinuteUsed = 0x0027,
            QueryMetrics = 0x0028,
            GlobalCommittedLSN = 0x0029,
            NumberOfReadRegions = 0x0030,
            OfferReplacePending = 0x0031,
            ItemLSN = 0x0032,
            RestoreState = 0x0033,
            CollectionSecurityIdentifier = 0x0034,
            TransportRequestID = 0x0035,
            ShareThroughput = 0x0036,
            // not used = 0x0037, LeaseSeqNumber
            DisableRntbdChannel = 0x0038,
            ServerDateTimeUtc = 0x0039,
            LocalLSN = 0x003A,
            QuorumAckedLocalLSN = 0x003B,
            ItemLocalLSN = 0x003C,
            HasTentativeWrites = 0x003D,
            SessionToken = 0x003E,
            ReplicatorLSNToGLSNDelta = 0x003F,
            ReplicatorLSNToLLSNDelta = 0x0040,
            VectorClockLocalProgress = 0x0041,
            MinimumRUsForOffer = 0x0042,
            XPConfigurationSessionsCount = 0x0043,
            IndexUtilization = 0x0044,
            QueryExecutionInfo = 0x0045,
            UnflishedMergeLogEntryCount = 0x0046,
            ResourceName = 0x0047,
            TimeToLiveInSeconds = 0x0048,
            ReplicaStatusRevoked = 0x0049,
            SoftMaxAllowedThroughput = 0x0050,
            BackendRequestDurationMilliseconds = 0x0051,
            CorrelatedActivityId = 0x0052,
            ConfirmedStoreChecksum = 0x0053,
            TentativeStoreChecksum = 0x0054,
            PendingPKDelete = 0x0055,
            AadAppliedRoleAssignmentId = 0x0056,
            CollectionUniqueIndexReIndexProgress = 0x0057,
            CollectionUniqueKeysUnderReIndex = 0x0058,
            AnalyticalMigrationProgress = 0x0059,
            TotalAccountThroughput = 0x005A,
            BYOKEncryptionProgress = 0x005B,
            AppliedPolicyElementId = 0x005C,
            MergeProgressBlocked = 0x005D,
            ChangeFeedInfo = 0x005E,
        }

        public sealed class Response : RntbdTokenStream<ResponseIdentifiers>
        {
            public RntbdToken payloadPresent;
            public RntbdToken lastStateChangeDateTime;
            public RntbdToken continuationToken;
            public RntbdToken eTag;

            public RntbdToken readsPerformed;
            public RntbdToken writesPerformed;
            public RntbdToken queriesPerformed;
            public RntbdToken indexTermsGenerated;
            public RntbdToken scriptsExecuted;
            public RntbdToken retryAfterMilliseconds;
            public RntbdToken indexingDirective;
            public RntbdToken storageMaxResoureQuota;
            public RntbdToken storageResourceQuotaUsage;
            public RntbdToken schemaVersion;
            public RntbdToken collectionPartitionIndex;
            public RntbdToken collectionServiceIndex;
            public RntbdToken LSN;
            public RntbdToken itemCount;
            public RntbdToken requestCharge;
            public RntbdToken ownerFullName;
            public RntbdToken ownerId;
            public RntbdToken databaseAccountId;
            public RntbdToken quorumAckedLSN;
            public RntbdToken requestValidationFailure;
            public RntbdToken subStatus;
            public RntbdToken collectionUpdateProgress;
            public RntbdToken currentWriteQuorum;
            public RntbdToken currentReplicaSetSize;
            public RntbdToken collectionLazyIndexProgress;
            public RntbdToken partitionKeyRangeId;
            public RntbdToken logResults;
            public RntbdToken xpRole;
            public RntbdToken isRUPerMinuteUsed;
            public RntbdToken queryMetrics;
            public RntbdToken queryExecutionInfo;
            public RntbdToken indexUtilization;
            public RntbdToken globalCommittedLSN;
            public RntbdToken numberOfReadRegions;
            public RntbdToken offerReplacePending;
            public RntbdToken itemLSN;
            public RntbdToken restoreState;
            public RntbdToken collectionSecurityIdentifier;
            public RntbdToken transportRequestID;
            public RntbdToken shareThroughput;
            public RntbdToken disableRntbdChannel;
            public RntbdToken serverDateTimeUtc;
            public RntbdToken localLSN;
            public RntbdToken quorumAckedLocalLSN;
            public RntbdToken itemLocalLSN;
            public RntbdToken hasTentativeWrites;
            public RntbdToken sessionToken;
            public RntbdToken replicatorLSNToGLSNDelta;
            public RntbdToken replicatorLSNToLLSNDelta;
            public RntbdToken vectorClockLocalProgress;
            public RntbdToken minimumRUsForOffer;
            public RntbdToken xpConfigurationSesssionsCount;
            public RntbdToken unflushedMergeLogEntryCount;
            public RntbdToken resourceName;
            public RntbdToken timeToLiveInSeconds;
            public RntbdToken replicaStatusRevoked;
            public RntbdToken softMaxAllowedThroughput;
            public RntbdToken backendRequestDurationMilliseconds;
            public RntbdToken correlatedActivityId;
            public RntbdToken confirmedStoreChecksum;
            public RntbdToken tentativeStoreChecksum;
            public RntbdToken pendingPKDelete;
            public RntbdToken aadAppliedRoleAssignmentId;
            public RntbdToken collectionUniqueIndexReIndexProgress;
            public RntbdToken collectionUniqueKeysUnderReIndex;
            public RntbdToken analyticalMigrationProgress;
            public RntbdToken totalAccountThroughput;
            public RntbdToken byokEncryptionProgress;
            public RntbdToken appliedPolicyElementId;
            public RntbdToken mergeProgressBlocked;
            public RntbdToken changeFeedInfo;

            public Response()
            {
                this.payloadPresent = new RntbdToken(true, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.PayloadPresent);
                this.lastStateChangeDateTime = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)ResponseIdentifiers.LastStateChangeDateTime);
                this.continuationToken = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.ContinuationToken);
                this.eTag = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.ETag);

                this.readsPerformed = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.ReadsPerformed);
                this.writesPerformed = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.WritesPerformed);
                this.queriesPerformed = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.QueriesPerformed);
                this.indexTermsGenerated = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.IndexTermsGenerated);
                this.scriptsExecuted = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.ScriptsExecuted);
                this.retryAfterMilliseconds = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.RetryAfterMilliseconds);
                this.indexingDirective = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.IndexingDirective);
                this.storageMaxResoureQuota = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.StorageMaxResoureQuota);
                this.storageResourceQuotaUsage = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.StorageResourceQuotaUsage);
                this.schemaVersion = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)ResponseIdentifiers.SchemaVersion);
                this.collectionPartitionIndex = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CollectionPartitionIndex);
                this.collectionServiceIndex = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CollectionServiceIndex);
                this.LSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.LSN);
                this.itemCount = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.ItemCount);
                this.requestCharge = new RntbdToken(false, RntbdTokenTypes.Double, (ushort)ResponseIdentifiers.RequestCharge);
                this.ownerFullName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.OwnerFullName);
                this.ownerId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.OwnerId);
                this.databaseAccountId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.DatabaseAccountId);
                this.quorumAckedLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.QuorumAckedLSN);
                this.requestValidationFailure = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.RequestValidationFailure);
                this.subStatus = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.SubStatus);
                this.collectionUpdateProgress = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CollectionUpdateProgress);
                this.currentWriteQuorum = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CurrentWriteQuorum);
                this.currentReplicaSetSize = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CurrentReplicaSetSize);
                this.collectionLazyIndexProgress = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CollectionLazyIndexProgress);
                this.partitionKeyRangeId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.PartitionKeyRangeId);
                this.logResults = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.LogResults);
                this.xpRole = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.XPRole);
                this.isRUPerMinuteUsed = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.IsRUPerMinuteUsed);
                this.queryMetrics = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.QueryMetrics);
                this.globalCommittedLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.GlobalCommittedLSN);
                this.numberOfReadRegions = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.NumberOfReadRegions);
                this.offerReplacePending = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.OfferReplacePending);
                this.itemLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.ItemLSN);
                this.restoreState = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.RestoreState);
                this.collectionSecurityIdentifier = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.CollectionSecurityIdentifier);
                this.transportRequestID = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.TransportRequestID);
                this.shareThroughput = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.ShareThroughput);
                this.disableRntbdChannel = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.DisableRntbdChannel);
                this.serverDateTimeUtc = new RntbdToken(false, RntbdTokenTypes.SmallString, (ushort)ResponseIdentifiers.ServerDateTimeUtc);
                this.localLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.LocalLSN);
                this.quorumAckedLocalLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.QuorumAckedLocalLSN);
                this.itemLocalLSN = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.ItemLocalLSN);
                this.hasTentativeWrites = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.HasTentativeWrites);
                this.sessionToken = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.SessionToken);
                this.replicatorLSNToGLSNDelta = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.ReplicatorLSNToGLSNDelta);
                this.replicatorLSNToLLSNDelta = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.ReplicatorLSNToLLSNDelta);
                this.vectorClockLocalProgress = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.VectorClockLocalProgress);
                this.minimumRUsForOffer = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.MinimumRUsForOffer);
                this.xpConfigurationSesssionsCount = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.XPConfigurationSessionsCount);
                this.indexUtilization = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.IndexUtilization);
                this.queryExecutionInfo = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.QueryExecutionInfo);
                this.unflushedMergeLogEntryCount = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.UnflishedMergeLogEntryCount);
                this.resourceName = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.ResourceName);
                this.timeToLiveInSeconds = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.TimeToLiveInSeconds);
                this.replicaStatusRevoked = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.ReplicaStatusRevoked);
                this.softMaxAllowedThroughput = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.SoftMaxAllowedThroughput);
                this.backendRequestDurationMilliseconds = new RntbdToken(false, RntbdTokenTypes.Double, (ushort)ResponseIdentifiers.BackendRequestDurationMilliseconds);
                this.correlatedActivityId = new RntbdToken(false, RntbdTokenTypes.Guid, (ushort)ResponseIdentifiers.CorrelatedActivityId);
                this.confirmedStoreChecksum = new RntbdToken(false, RntbdTokenTypes.ULongLong, (ushort)ResponseIdentifiers.ConfirmedStoreChecksum);
                this.tentativeStoreChecksum = new RntbdToken(false, RntbdTokenTypes.ULongLong, (ushort)ResponseIdentifiers.TentativeStoreChecksum);
                this.pendingPKDelete = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.PendingPKDelete);
                this.aadAppliedRoleAssignmentId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.AadAppliedRoleAssignmentId);
                this.collectionUniqueIndexReIndexProgress = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.CollectionUniqueIndexReIndexProgress);
                this.collectionUniqueKeysUnderReIndex = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.CollectionUniqueKeysUnderReIndex);
                this.analyticalMigrationProgress = new RntbdToken(false, RntbdTokenTypes.ULong, (ushort)ResponseIdentifiers.AnalyticalMigrationProgress);
                this.totalAccountThroughput = new RntbdToken(false, RntbdTokenTypes.LongLong, (ushort)ResponseIdentifiers.TotalAccountThroughput);
                this.byokEncryptionProgress = new RntbdToken(false, RntbdTokenTypes.Long, (ushort)ResponseIdentifiers.BYOKEncryptionProgress);
                this.appliedPolicyElementId = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.AppliedPolicyElementId);
                this.mergeProgressBlocked = new RntbdToken(false, RntbdTokenTypes.Byte, (ushort)ResponseIdentifiers.MergeProgressBlocked);
                this.changeFeedInfo = new RntbdToken(false, RntbdTokenTypes.String, (ushort)ResponseIdentifiers.ChangeFeedInfo);
                base.SetTokens(new RntbdToken[]
                {
                    this.payloadPresent,
                    this.lastStateChangeDateTime,
                    this.continuationToken,
                    this.eTag,
                    this.readsPerformed,
                    this.writesPerformed,
                    this.queriesPerformed,
                    this.indexTermsGenerated,
                    this.scriptsExecuted,
                    this.retryAfterMilliseconds,
                    this.indexingDirective,
                    this.storageMaxResoureQuota,
                    this.storageResourceQuotaUsage,
                    this.schemaVersion,
                    this.collectionPartitionIndex,
                    this.collectionServiceIndex,
                    this.LSN,
                    this.itemCount,
                    this.requestCharge,
                    this.ownerFullName,
                    this.ownerId,
                    this.databaseAccountId,
                    this.quorumAckedLSN,
                    this.requestValidationFailure,
                    this.subStatus,
                    this.collectionUpdateProgress,
                    this.currentWriteQuorum,
                    this.currentReplicaSetSize,
                    this.collectionLazyIndexProgress,
                    this.partitionKeyRangeId,
                    this.logResults,
                    this.xpRole,
                    this.isRUPerMinuteUsed,
                    this.queryMetrics,
                    this.globalCommittedLSN,
                    this.numberOfReadRegions,
                    this.offerReplacePending,
                    this.itemLSN,
                    this.restoreState,
                    this.collectionSecurityIdentifier,
                    this.transportRequestID,
                    this.shareThroughput,
                    this.disableRntbdChannel,
                    this.serverDateTimeUtc,
                    this.localLSN,
                    this.quorumAckedLocalLSN,
                    this.itemLocalLSN,
                    this.hasTentativeWrites,
                    this.sessionToken,
                    this.replicatorLSNToGLSNDelta,
                    this.replicatorLSNToLLSNDelta,
                    this.vectorClockLocalProgress,
                    this.minimumRUsForOffer,
                    this.xpConfigurationSesssionsCount,
                    this.indexUtilization,
                    this.queryExecutionInfo,
                    this.unflushedMergeLogEntryCount,
                    this.resourceName,
                    this.timeToLiveInSeconds,
                    this.replicaStatusRevoked,
                    this.softMaxAllowedThroughput,
                    this.backendRequestDurationMilliseconds,
                    this.correlatedActivityId,
                    this.confirmedStoreChecksum,
                    this.tentativeStoreChecksum,
                    this.pendingPKDelete,
                    this.aadAppliedRoleAssignmentId,
                    this.collectionUniqueIndexReIndexProgress,
                    this.collectionUniqueKeysUnderReIndex,
                    this.analyticalMigrationProgress,
                    this.totalAccountThroughput,
                    this.byokEncryptionProgress,
                    this.appliedPolicyElementId,
                    this.mergeProgressBlocked,
                    this.changeFeedInfo,
                });
            }
        }

        //
        // DEVNOTE: This enumeration is used only for the logging purpose.  Do not use it for any other purpose.
        //
        public enum CallerId : byte
        {
            /// <summary>
            /// The default caller Id
            /// </summary>
            Anonymous = 0x00,

            /// <summary>
            /// The connection request is made by Gateway
            /// </summary>
            Gateway = 0x01,

            /// <summary>
            /// The connection request is made by BackgroundTask
            /// </summary>
            BackgroundTask = 0x02,

            /// <summary>
            /// The connection request is made by ManagementWorkerRuntime
            /// </summary>
            ManagementWorker = 0x03,

            /// <summary>
            /// Invalid caller Id
            /// </summary>
            Invalid = 0x04,
        }

        internal sealed class RntbdEntityPool<T, TU>
        where T : RntbdTokenStream<TU>, new()
        where TU : Enum
        {
            public static readonly RntbdEntityPool<T, TU> Instance = new RntbdEntityPool<T, TU>();

            private readonly ConcurrentQueue<T> entities = new ConcurrentQueue<T>();

            private RntbdEntityPool()
            {
            }

            public EntityOwner Get()
            {
                if (this.entities.TryDequeue(out T entity))
                {
                    return new EntityOwner(entity);
                }

                return new EntityOwner(new T());
            }

            private void Return(T entity)
            {
                entity.Reset();
                this.entities.Enqueue(entity);
            }

            public readonly struct EntityOwner : IDisposable
            {
                public EntityOwner(T entity)
                {
                    this.Entity = entity;
                }

                public T Entity { get; }

                public void Dispose()
                {
                    if (this.Entity != null)
                    {
                        RntbdEntityPool<T, TU>.Instance.Return(this.Entity);
                    }
                }
            }
        }
    }
}
